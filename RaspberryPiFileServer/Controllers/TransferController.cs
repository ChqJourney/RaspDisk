using Microsoft.AspNetCore.Mvc;
using System.IO;
using RaspberryPiFileServer.Services;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using RaspberryPiFileServer.Hubs;

namespace RaspberryPiFileServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransferController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;
        private readonly string _transferPath;
        private readonly ILogger<TransferController> _logger;
        private readonly IHubContext<TransferHub> _hubContext;

        public TransferController(IConfiguration configuration, ILogger<TransferController> logger, IHubContext<TransferHub> hubContext)
        {
            _configuration = configuration;
            _logger = logger;
            _hubContext = hubContext;
            _uploadPath = _configuration["UploadPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), 
            "Uploads");
            _transferPath = Path.Combine(Path.GetDirectoryName(_uploadPath), "transfer");

            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string path = "")
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("文件上传失败：未提供文件");
                return BadRequest("No file uploaded");
            }

            var targetPath = Path.Combine(_transferPath, path ?? "");
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            var filePath = Path.Combine(targetPath, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                var buffer = new byte[8192];
                var totalBytes = file.Length;
                var bytesRead = 0L;

                using (var fileStream = file.OpenReadStream())
                {
                    while (bytesRead < totalBytes)
                    {
                        var currentRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);
                        if (currentRead == 0) break;

                        await stream.WriteAsync(buffer, 0, currentRead);
                        bytesRead += currentRead;

                        var progress = (int)((bytesRead * 100) / totalBytes);
                        await _hubContext.Clients.All.SendAsync("ReceiveTransferProgress", file.FileName, progress);
                    }
                }
            }

            await _hubContext.Clients.All.SendAsync("ReceiveTransferComplete", file.FileName);
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", $"文件上传成功：{file.FileName}");

            _logger.LogInformation("文件上传成功：{FileName}", file.FileName);
            return Ok(new { fileName = file.FileName });
        }

        [HttpGet("download/{fileName}")]
        public IActionResult Download(string fileName)
        {
            var filePath = Path.Combine(_transferPath, fileName);
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("文件下载失败：文件不存在 {FileName}", fileName);
                return NotFound("File not found");
            }

            _logger.LogInformation("文件下载：{FileName}", fileName);
            var fileInfo = new FileInfo(filePath);
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var totalBytes = fileInfo.Length;
            
            Response.OnCompleted(async () => {
                await _hubContext.Clients.All.SendAsync("ReceiveTransferComplete", fileName);
                fileStream.Dispose();
            });

            return File(fileStream, "application/octet-stream", fileName);
        }

        [HttpGet("list")]
        public IActionResult ListFiles([FromQuery] string path = "")
        {
            var targetPath = Path.Combine(_transferPath, path ?? "");
            if (!Directory.Exists(targetPath))
            {
                _logger.LogWarning("获取文件列表失败：目录不存在 {Path}", path);
                return NotFound("Directory not found");
            }

            var files = Directory.GetFiles(targetPath)
                .Select(f => new FileInfo(f))
                .Select(f => new
                {
                    name = f.Name,
                    size = f.Length,
                    lastModified = f.LastWriteTime
                });

            _logger.LogInformation("获取文件列表：{Path}", path);
            return Ok(files);
        }
    }
}