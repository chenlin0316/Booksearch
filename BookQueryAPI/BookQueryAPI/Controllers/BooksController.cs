using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookQueryAPI.Models;
using Microsoft.Extensions.Caching.Distributed;
using BookQueryAPI.DTO;
using Newtonsoft.Json;
using System.Text;
using StackExchange.Redis;
using System.Xml.Linq;

namespace BookQueryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly BookDBContext _context;
        private readonly IDistributedCache _cache;

        public BooksController(BookDBContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        //資料
        public static BookDTOClass ToBookDTO(Book book) 
        {

            return new BookDTOClass
            {
                ID = book.ID,
                書名 = book.書名,
                作者 = book.作者,
                出版日期 = book.出版日期,
                簡介 = book.簡介
            };
        }

        // GET: api/Books
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Book>>> GetBook()
        {
            var cacheKey = "bookList";
            var bookListBytes = await _cache.GetAsync(cacheKey);
            if (bookListBytes!= null) 
            {
                //取得快取
                var CacheBookList = Encoding.UTF8.GetString(bookListBytes);
                var bookList = JsonConvert.DeserializeObject<List<BookDTOClass>>(CacheBookList);
                return Ok(bookList);
            }
            var books = await _context.Book.ToListAsync();
            if (books == null)
            {
                return NotFound();
            }
            var BookDTOList = books.Select(a => ToBookDTO(a)).ToList();

            // 存入快取
            var bookListJson = JsonConvert.SerializeObject(BookDTOList);
            var bookListBytesToCache = Encoding.UTF8.GetBytes(bookListJson);
            var cacheEntryOptions = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            await _cache.SetAsync(cacheKey, bookListBytesToCache, cacheEntryOptions);

            return Ok(BookDTOList);
        }

        // GET: api/Books/5
        [HttpGet("Search")]
        public async Task<ActionResult<Book>> GetBook(int? id, string? 書名, string? 作者, DateTime? 開始日期, DateTime? 結束日期)
        {
            //從cache比對
            var cacheKey = "bookList";
            var bookListBytes = await _cache.GetAsync(cacheKey);

            if (bookListBytes != null) {
                var cacheBookList = Encoding.UTF8.GetString(bookListBytes);
                var bookFromcahe = JsonConvert.DeserializeObject<List<BookDTOClass>>(cacheBookList);

                if (id != null)
                {
                    bookFromcahe = bookFromcahe.Where(a => a.ID == id).ToList();
                }
                if (!string.IsNullOrEmpty(書名))
                {
                    bookFromcahe = bookFromcahe.Where(a => a.書名 == 書名).ToList();
                }
                if (!string.IsNullOrEmpty(作者))
                {
                    bookFromcahe = bookFromcahe.Where(a => a.作者 == 作者).ToList();
                }
                if (開始日期.HasValue)
                {
                    bookFromcahe = bookFromcahe.Where(a => a.出版日期 >= 開始日期).ToList();
                }
                if (結束日期.HasValue)
                {
                    bookFromcahe = bookFromcahe.Where(a => a.出版日期 <= 結束日期).ToList();
                }
                return Ok(bookFromcahe);
            }

            var booksQuery = _context.Book.AsQueryable();
            if (id != null) {
                booksQuery = booksQuery.Where(a => a.ID == id);
            }
            if (!string.IsNullOrEmpty(書名)) {
                booksQuery = booksQuery.Where(a => a.書名 == 書名);
            }
            if (!string.IsNullOrEmpty(作者)) {
                booksQuery = booksQuery.Where(a => a.作者 == 作者);
            }
            if (開始日期.HasValue) {
                booksQuery = booksQuery.Where(a => a.出版日期 >= 開始日期);
            }
            if (結束日期.HasValue)
            {
                booksQuery = booksQuery.Where(a => a.出版日期 <= 結束日期);
            }

            var book = await booksQuery.ToListAsync();

            if (book.Count == 0)
            {
                return NotFound();
            }

            var BookDTOList = book.Select(a => ToBookDTO(a)).ToList();

            // 存入快取
            var bookListJson = JsonConvert.SerializeObject(BookDTOList);
            var bookListBytesToCache = Encoding.UTF8.GetBytes(bookListJson);
            var cacheEntryOptions = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            await _cache.SetAsync("bookList", bookListBytesToCache, cacheEntryOptions);
            
            return Ok(BookDTOList);
        }

        // PUT: api/Books/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBook(int id, BookDTOClass book)
        {
            var updated = await _context.Book.FindAsync(id);

            if (updated == null)
            {
                return NotFound();
            }
            book.ID = id;
            _context.Entry(updated).CurrentValues.SetValues(book);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            //更新cache
            var cacheKey = "bookList";
            var bookList = await _context.Book.Select(x => ToBookDTO(x)).ToListAsync();
            var json = JsonConvert.SerializeObject(bookList);
            await _cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(json));

            return NoContent();
        }

        // POST: api/Books
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Book>> PostBook(BookDTOClass book)
        {
          if (_context.Book == null)
          {
              return Problem("Entity set 'BookDBContext.Book'  is null.");
          }
            Book newbook = new Book() {
                書名 = book.書名,
                作者 = book.作者,
                出版日期 = book.出版日期,
                簡介 = book.簡介
            };
            _context.Book.Add(newbook);
            await _context.SaveChangesAsync();

            //更新cache
            var cacheKey = "bookList";
            var bookList = await _context.Book.Select(x => ToBookDTO(x)).ToListAsync();
            var json = JsonConvert.SerializeObject(bookList);
            await _cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(json));

            return CreatedAtAction("GetBook", new { id = newbook.ID }, newbook);
        }

        // DELETE: api/Books/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            if (_context.Book == null)
            {
                return NotFound();
            }
            var book = await _context.Book.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            _context.Book.Remove(book);
            await _context.SaveChangesAsync();

            //更新cache
            var cacheKey = "bookList";
            var bookList = await _context.Book.Select(x => ToBookDTO(x)).ToListAsync();
            var json = JsonConvert.SerializeObject(bookList);
            await _cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(json));

            return NoContent();
        }

        private bool BookExists(int id)
        {
            return (_context.Book?.Any(e => e.ID == id)).GetValueOrDefault();
        }
    }
}
