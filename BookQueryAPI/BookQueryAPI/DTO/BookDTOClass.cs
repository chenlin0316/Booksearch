namespace BookQueryAPI.DTO
{
    public class BookDTOClass
    {
        public int ID { get; set; }
        public string? 書名 { get; set; }
        public string? 作者 { get; set; }
        public DateTime? 出版日期 { get; set; }
        public string? 簡介 { get; set; }
    }
}
