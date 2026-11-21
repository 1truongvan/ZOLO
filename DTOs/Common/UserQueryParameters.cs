namespace WebApplication1.DTOs.Common
{
    public class UserQueryParameters
    {
        public string? SearchTerm { get; set; }

        public string? Role { get; set; }

        public string? SortBy { get; set; }

        public bool IsDescending { get; set; }

        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int PageSize { get; set; } = 10;
    }
}
