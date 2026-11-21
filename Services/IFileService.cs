using static WebApplication1.DTOs.FileUploadDbDto;
using Microsoft.AspNetCore.Http;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public interface IFileService
    {
        Task<FileResponseDto> UploadFileAsync(IFormFile file);
        Task<FileDownloadDto?> GetFileForDownloadAsync(int id);
    }
}
