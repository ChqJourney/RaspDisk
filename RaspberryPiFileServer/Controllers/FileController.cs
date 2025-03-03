using Microsoft.AspNetCore.Mvc;
using System.IO;
using RaspberryPiFileServer.Services;
using System.Text.Json;
using System.Collections.Concurrent;

namespace RaspberryPiFileServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;
        private readonly ILogger<FileController> _logger;

        private string GetUniqueFilePath(string originalPath)
        {
            if (!System.IO.File.Exists(originalPath))
            {
                return originalPath;
            }

            string directory = Path.GetDirectoryName(originalPath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            return Path.Combine(directory, $"{fileNameWithoutExt}_{timestamp}{extension}");
        }

        private readonly string _tempPath;

        public FileController(IConfiguration configuration, ILogger<FileController> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _uploadPath = _configuration["UploadPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            _tempPath = Path.Combine(Path.GetDirectoryName(_uploadPath), "temp");
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
        }
        //重命名
        [HttpPut("rename")]
        public async Task<IActionResult> RenameItem([FromBody] RenameRequest request)
        {
            if (string.IsNullOrEmpty(request?.PathWithOldName) || string.IsNullOrEmpty(request?.PathWithNewName))
            {
                _logger.LogWarning("重命名失败：源路径或新路径未提供");
                return BadRequest("Source and new paths are required");
            }

            var sourcePath = Path.Combine(_uploadPath, request.PathWithOldName);
            var destinationPath = Path.Combine(_uploadPath, request.PathWithNewName);

            if (!IsPathSafe(sourcePath) || !IsPathSafe(destinationPath))
            {
                _logger.LogWarning("重命名失败：非法路径 源：{SourcePath} 目标：{DestinationPath}", 
                    request.PathWithOldName, request.PathWithNewName);
                return BadRequest("Invalid path");
            }

            if (!System.IO.File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                _logger.LogWarning("重命名失败：源文件或目录不存在 {Path}", request.PathWithOldName);
                return NotFound("Source file or directory not found");
            }

            try
            {
                if (System.IO.File.Exists(sourcePath))
                {
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    System.IO.File.Move(sourcePath, destinationPath, true);
                }
                else
                {
                    Directory.Move(sourcePath, destinationPath);
                }

                _logger.LogInformation("重命名成功：从 {SourcePath} 到 {DestinationPath}", 
                    request.PathWithOldName, request.PathWithNewName);
                return Ok(new { message = "Rename successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重命名失败：从 {SourcePath} 到 {DestinationPath}", 
                    request.PathWithOldName, request.PathWithNewName);
                return StatusCode(500, "Failed to rename file or directory");
            }
        }

        public class RenameRequest
        {
            public string PathWithOldName { get; set; }
            public string PathWithNewName { get; set; }
        }
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string path = "")
        {   
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("文件上传失败：未提供文件");
                return BadRequest("No file uploaded");
            }

            const long maxFileSize = 85L * 1024 * 1024; // 30MB
            if (file.Length > maxFileSize)
            {
                _logger.LogWarning("文件上传失败：文件大小超过限制 {FileSize} bytes", file.Length);
                return BadRequest("File size exceeds 35MB limit. Please use large file upload API.");
            }

            var targetPath = Path.Combine(_uploadPath, path ?? "");
            if (!IsPathSafe(targetPath))
            {
                _logger.LogWarning("文件上传失败：非法路径 {Path}", path);
                return BadRequest("Invalid path");
            }

            if (!Directory.Exists(targetPath))
            {
                try
                {
                    Directory.CreateDirectory(targetPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "文件上传失败：创建目录失败 {Path}", path);
                    return StatusCode(500, "Failed to create directory");
                }
            }

            var filePath = Path.Combine(targetPath, file.FileName);
            var uniqueFilePath = GetUniqueFilePath(filePath);
            using (var stream = new FileStream(uniqueFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("文件上传成功：{FileName}，大小：{FileSize} bytes，路径：{Path}", 
                Path.GetFileName(uniqueFilePath), file.Length, path);
            return Ok(new { fileName = Path.GetFileName(uniqueFilePath) });
        }

        [HttpPost("upload/large/init")]
        public async Task<IActionResult> InitLargeFileUpload([FromBody] InitLargeFileUploadRequest request)
        {   
            if (string.IsNullOrEmpty(request?.FileName))
            {
                _logger.LogWarning("大文件上传初始化失败：未提供文件名");
                return BadRequest("File name is required");
            }

            var uploadId = Guid.NewGuid().ToString();
            var uploadInfo = new LargeFileUploadInfo
            {
                FileName = Path.Combine(request.Directory,request.FileName),
                TotalSize = request.TotalSize,
                ChunkSize = request.ChunkSize,
                UploadedChunks = new List<int>(),
                CreatedTime = DateTime.UtcNow
            };
            Console.WriteLine(uploadInfo.FileName);
            var tempDir = Path.Combine(_tempPath, uploadId);
            Directory.CreateDirectory(tempDir);
            System.IO.File.WriteAllText(
                Path.Combine(tempDir, "upload_info.json"), 
                System.Text.Json.JsonSerializer.Serialize(uploadInfo)
            );

            _logger.LogInformation("大文件上传初始化成功：{FileName}，总大小：{TotalSize} bytes，上传ID：{UploadId}", 
                request.FileName, request.TotalSize, uploadId);
            return Ok(new { uploadId });
        }

        private static readonly ConcurrentDictionary<string, bool> _pausedUploads = new ConcurrentDictionary<string, bool>();

        [HttpPost("upload/large/pause/{uploadId}")]
        public IActionResult PauseUpload(string uploadId, [FromBody] bool pause)
        {   
            var tempDir = Path.Combine(_tempPath, uploadId);
            if (!Directory.Exists(tempDir))
            {   
                _logger.LogWarning("暂停上传失败：上传会话不存在，上传ID：{UploadId}", uploadId);
                return NotFound("Upload session not found");
            }

            if (pause)
            {
                _pausedUploads.TryAdd(uploadId, true);
                _logger.LogInformation("上传已暂停：上传ID：{UploadId}", uploadId);
            }
            else
            {
                _pausedUploads.TryRemove(uploadId, out _);
                _logger.LogInformation("上传已恢复：上传ID：{UploadId}", uploadId);
            }

            return Ok(new { status = pause ? "paused" : "resumed" });
        }

        [HttpPost("upload/large/stop/{uploadId}")]
        public IActionResult StopUpload(string uploadId)
        {   
            var tempDir = Path.Combine(_tempPath, uploadId);
            if (!Directory.Exists(tempDir))
            {   
                _logger.LogWarning("停止上传失败：上传会话不存在，上传ID：{UploadId}", uploadId);
                return NotFound("Upload session not found");
            }

            try
            {
                Directory.Delete(tempDir, true);
                _pausedUploads.TryRemove(uploadId, out _);
                _logger.LogInformation("上传已停止并清理：上传ID：{UploadId}", uploadId);
                return Ok(new { status = "stopped" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理上传文件失败：上传ID：{UploadId}", uploadId);
                return StatusCode(500, "Failed to clean up upload files");
            }
        }

        [HttpPost("upload/large/chunk/{uploadId}")]
        public async Task<IActionResult> UploadChunk(string uploadId, [FromForm] int chunkNumber, IFormFile chunk)
        {   
            if (_pausedUploads.TryGetValue(uploadId, out bool isPaused) && isPaused)
            {
                _logger.LogInformation("上传已暂停，拒绝新的分片：上传ID：{UploadId}，分片编号：{ChunkNumber}", 
                    uploadId, chunkNumber);
                return BadRequest(new { status = "paused" });
            }
            var tempDir = Path.Combine(_tempPath, uploadId);
            if (!Directory.Exists(tempDir))
            {
                _logger.LogWarning("分片上传失败：上传会话不存在，上传ID：{UploadId}", uploadId);
                return NotFound("Upload session not found");
            }

            var uploadInfoPath = Path.Combine(tempDir, "upload_info.json");
            if (!System.IO.File.Exists(uploadInfoPath))
            {
                _logger.LogWarning("分片上传失败：上传信息文件不存在，上传ID：{UploadId}", uploadId);
                return NotFound("Upload information not found");
            }

            var uploadInfoJson = System.IO.File.ReadAllText(uploadInfoPath);
            if (string.IsNullOrEmpty(uploadInfoJson))
            {
                _logger.LogWarning("分片上传失败：上传信息文件为空，上传ID：{UploadId}", uploadId);
                return NotFound("Upload information is empty");
            }
            var uploadInfo = System.Text.Json.JsonSerializer.Deserialize<LargeFileUploadInfo>(
                uploadInfoJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            if (uploadInfo == null)
            {
                _logger.LogWarning("分片上传失败：上传信息反序列化失败，上传ID：{UploadId}", uploadId);
                return NotFound("Failed to deserialize upload information");
            }

            if (chunk == null || chunk.Length == 0)
            {
                _logger.LogWarning("分片上传失败：分片数据为空，上传ID：{UploadId}，分片编号：{ChunkNumber}", 
                    uploadId, chunkNumber);
                return BadRequest("No chunk data");
            }

            if (chunk.Length > uploadInfo.ChunkSize)
            {
                _logger.LogWarning("分片上传失败：分片大小超过限制，上传ID：{UploadId}，分片编号：{ChunkNumber}", 
                    uploadId, chunkNumber);
                return BadRequest("Chunk size exceeds the defined size");
            }

            var chunkPath = Path.Combine(tempDir, $"chunk_{chunkNumber}");
            using (var stream = new FileStream(chunkPath, FileMode.Create))
            {
                await chunk.CopyToAsync(stream);
            }

            uploadInfo.UploadedChunks.Add(chunkNumber);
            System.IO.File.WriteAllText(
                uploadInfoPath,
                System.Text.Json.JsonSerializer.Serialize(uploadInfo)
            );

            var expectedChunks = Math.Ceiling((double)uploadInfo.TotalSize / uploadInfo.ChunkSize);
            var isComplete = uploadInfo.UploadedChunks.Count == expectedChunks;

            if (isComplete)
            {
                var finalPath = Path.Combine(_uploadPath, uploadInfo.FileName);
                var uniqueFinalPath = GetUniqueFilePath(finalPath);
                using (var destination = new FileStream(uniqueFinalPath, FileMode.Create))
                {
                    for (int i = 0; i < expectedChunks; i++)
                    {
                        var currentChunkPath = Path.Combine(tempDir, $"chunk_{i}");
                        using var source = new FileStream(currentChunkPath, FileMode.Open);
                        await source.CopyToAsync(destination);
                    }
                }

                Directory.Delete(tempDir, true);
                _logger.LogInformation("大文件上传完成：{FileName}，总大小：{TotalSize} bytes，上传ID：{UploadId}", 
                    Path.GetFileName(uniqueFinalPath), uploadInfo.TotalSize, uploadId);
                return Ok(new { status = "completed", fileName = Path.GetFileName(uniqueFinalPath) });
            }

            _logger.LogDebug("分片上传成功：上传ID：{UploadId}，分片编号：{ChunkNumber}，总分片数：{TotalChunks}", 
                uploadId, chunkNumber, expectedChunks);
            return Ok(new { status = "in_progress", uploadedChunks = uploadInfo.UploadedChunks });
        }

        [HttpGet("upload/large/status/{uploadId}")]
        public async Task<IActionResult> GetUploadStatus(string uploadId)
        {   
            var tempDir = Path.Combine(_tempPath, uploadId);
            if (!Directory.Exists(tempDir))
            {
                _logger.LogWarning("获取上传状态失败：上传会话不存在，上传ID：{UploadId}", uploadId);
                return NotFound("Upload session not found");
            }

            var uploadInfoPath = Path.Combine(tempDir, "upload_info.json");
            if (!System.IO.File.Exists(uploadInfoPath))
            {
                _logger.LogWarning("获取上传状态失败：上传信息文件不存在，上传ID：{UploadId}", uploadId);
                return NotFound("Upload information not found");
            }

            var uploadInfo = System.Text.Json.JsonSerializer.Deserialize<LargeFileUploadInfo>(
                System.IO.File.ReadAllText(uploadInfoPath)
            );

            _logger.LogDebug("获取上传状态：{FileName}，已上传分片数：{UploadedChunks}，上传ID：{UploadId}", 
                uploadInfo.FileName, uploadInfo.UploadedChunks.Count, uploadId);
            return Ok(new
            {
                fileName = uploadInfo.FileName,
                totalSize = uploadInfo.TotalSize,
                uploadedChunks = uploadInfo.UploadedChunks,
                status = "in_progress"
            });
        }

        public class InitLargeFileUploadRequest
        {
            public string Directory{get;set;}
            public string FileName { get; set; }
            public long TotalSize { get; set; }
            public int ChunkSize { get; set; } = 5 * 1024 * 1024; // 默认5MB
        }

        public class CreateDirectoryRequest
        {
            public string Path { get; set; }
        }

        public class MoveFileRequest
        {
            public string SourcePath { get; set; }
            public string DestinationPath { get; set; }
        }

        private class LargeFileUploadInfo
        {
            public string FileName { get; set; }
            public long TotalSize { get; set; }
            public int ChunkSize { get; set; }
            public List<int> UploadedChunks { get; set; }
            public DateTime CreatedTime { get; set; }
        }

        private static readonly Dictionary<string, string> _mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {".txt", "text/plain"},
            {".pdf", "application/pdf"},
            {".doc", "application/msword"},
            {".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
            {".xls", "application/vnd.ms-excel"},
            {".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
            {".png", "image/png"},
            {".jpg", "image/jpeg"},
            {".jpeg", "image/jpeg"},
            {".gif", "image/gif"},
            {".csv", "text/csv"},
            {".mp3", "audio/mpeg"},
            {".mp4", "video/mp4"},
            {".zip", "application/zip"},
            {".rar", "application/x-rar-compressed"},
            {".7z", "application/x-7z-compressed"},
            {".json", "application/json"},
            {".xml", "application/xml"},
            {".html", "text/html"},
            {".css", "text/css"},
            {".js", "application/javascript"}
        };

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return _mimeTypes.TryGetValue(extension, out var mimeType) ? mimeType : "application/octet-stream";
        }

        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> Download(string fileName)
        {
            var filePath = Path.Combine(_uploadPath, fileName);
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("文件下载失败：文件不存在，文件名：{FileName}", fileName);
                return NotFound();
            }

            var mimeType = GetMimeType(fileName);
            _logger.LogInformation("文件下载成功：{FileName}，大小：{FileSize} bytes，MIME类型：{MimeType}", 
                fileName, new FileInfo(filePath).Length, mimeType);
            var fileStream = System.IO.File.OpenRead(filePath);
            return File(fileStream, mimeType, fileName);
        }

        private bool IsPathSafe(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                return fullPath.StartsWith(_uploadPath) || fullPath.StartsWith(_tempPath);
            }
            catch
            {
                return false;
            }
        }

        [HttpGet("list")]
        public async Task<IActionResult> ListFiles([FromQuery] string path = "", [FromQuery] string searchQuery = "")
        {   
            var targetPath = Path.Combine(_uploadPath, path ?? "");
            if (!IsPathSafe(targetPath))
            {   
                _logger.LogWarning("访问目录失败：非法路径 {Path}", path);
                return BadRequest("Invalid path");
            }

            if (!Directory.Exists(targetPath))
            {   
                _logger.LogWarning("访问目录失败：目录不存在 {Path}", path);
                return NotFound("Directory not found");
            }

            var items = new List<object>();
            var searchPattern = searchQuery?.Trim() ?? "";

            // 获取目录，根据是否有搜索条件决定是否递归
            var searchOption = string.IsNullOrEmpty(searchPattern) ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            var directories = Directory.GetDirectories(targetPath, "*", searchOption)
                .Where(d => string.IsNullOrEmpty(searchPattern) || 
                           Path.GetFileName(d).Replace(" ", "").Contains(searchPattern.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                .Select(d => new
                {
                    name = Path.GetFileName(d),
                    type = "directory",
                    path = Path.GetRelativePath(targetPath, d),
                    lastModified = Directory.GetLastWriteTime(d)
                });
            items.AddRange(directories);

            // 获取文件，同样根据是否有搜索条件决定是否递归
            var files = Directory.GetFiles(targetPath, "*", searchOption)
                .Where(f => string.IsNullOrEmpty(searchPattern) || 
                           Path.GetFileName(f).Replace(" ", "").Contains(searchPattern.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                .Select(f => new
                {
                    name = Path.GetFileName(f),
                    type = "file",
                    path = Path.GetRelativePath(targetPath, f),
                    size = new FileInfo(f).Length,
                    lastModified = System.IO.File.GetLastWriteTime(f)
                });
            items.AddRange(files);
            var sortedItems = items.OrderBy(item => ((dynamic)item).name).ToList();
            _logger.LogInformation("获取目录列表成功：{Path}，搜索关键词：{SearchQuery}，共{ItemCount}个项目", 
                path, searchQuery, items.Count);
            return Ok(sortedItems);
        }

        [HttpPost("directory")]
        public async Task<IActionResult> CreateDirectory([FromBody] CreateDirectoryRequest request)
        {
            if (string.IsNullOrEmpty(request?.Path))
            {
                _logger.LogWarning("创建目录失败：未提供路径");
                return BadRequest("Path is required");
            }

            var targetPath = Path.Combine(_uploadPath, request.Path);
            if (!IsPathSafe(targetPath))
            {
                _logger.LogWarning("创建目录失败：非法路径 {Path}", request.Path);
                return BadRequest("Invalid path");
            }

            if (Directory.Exists(targetPath))
            {
                _logger.LogWarning("创建目录失败：目录已存在 {Path}", request.Path);
                return BadRequest("Directory already exists");
            }

            try
            {
                Directory.CreateDirectory(targetPath);
                _logger.LogInformation("创建目录成功：{Path}", request.Path);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建目录失败：{Path}", request.Path);
                return StatusCode(500, "Failed to create directory");
            }
        }

        [HttpPost("move")]
        public async Task<IActionResult> MoveFile([FromBody] MoveFileRequest request)
        {
            if (string.IsNullOrEmpty(request?.SourcePath) || string.IsNullOrEmpty(request?.DestinationPath))
            {
                _logger.LogWarning("移动文件失败：源路径或目标路径未提供");
                return BadRequest("Source and destination paths are required");
            }

            var sourcePath = Path.Combine(_uploadPath, request.SourcePath);
            var destinationPath = Path.Combine(_uploadPath, request.DestinationPath);

            if (!IsPathSafe(sourcePath) || !IsPathSafe(destinationPath))
            {
                _logger.LogWarning("移动文件失败：非法路径 源：{SourcePath} 目标：{DestinationPath}", 
                    request.SourcePath, request.DestinationPath);
                return BadRequest("Invalid path");
            }

            if (!System.IO.File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                _logger.LogWarning("移动文件失败：源文件或目录不存在 {Path}", request.SourcePath);
                return NotFound("Source file or directory not found");
            }

            try
            {
                if (System.IO.File.Exists(sourcePath))
                {
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    System.IO.File.Move(sourcePath, destinationPath, true);
                }
                else
                {
                    Directory.Move(sourcePath, destinationPath);
                }

                _logger.LogInformation("移动文件成功：从 {SourcePath} 到 {DestinationPath}", 
                    request.SourcePath, request.DestinationPath);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "移动文件失败：从 {SourcePath} 到 {DestinationPath}", 
                    request.SourcePath, request.DestinationPath);
                return StatusCode(500, "Failed to move file or directory");
            }
        }

        [HttpGet("test")]
        public IActionResult Test(){
            return Ok("ok");
        }

        [HttpGet("recent")]
        public IActionResult GetRecentFiles()
        {
            var items = new List<object>();
            var searchOption = SearchOption.AllDirectories;
            var cutoffDate = DateTime.Now.AddDays(-7);

            var files = Directory.GetFiles(_uploadPath, "*", searchOption)
                .Where(f => System.IO.File.GetLastWriteTime(f) >= cutoffDate)
                .Select(f => new
                {
                    name = Path.GetFileName(f),
                    type = "file",
                    path = Path.GetRelativePath(_uploadPath, f),
                    size = new FileInfo(f).Length,
                    lastModified = System.IO.File.GetLastWriteTime(f)
                });

            items.AddRange(files);
            var sortedItems = items.OrderByDescending(item => ((dynamic)item).lastModified).ToList();

            _logger.LogInformation("获取最近7天修改的文件列表：共{ItemCount}个文件", items.Count);
            return Ok(sortedItems);
        }

        [HttpDelete("directory/{*folderPath}")]
        public IActionResult DeleteDirectory(string folderPath)
        {
            var fullPath = Path.Combine(_uploadPath, folderPath);
            if (!Directory.Exists(fullPath))
            {
                _logger.LogWarning("文件夹删除失败：文件夹不存在，路径：{FolderPath}", folderPath);
                return NotFound();
            }

            try
            {
                Directory.Delete(fullPath, true); // true表示递归删除
                _logger.LogInformation("文件夹删除成功：{FolderPath}", folderPath);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件夹删除失败：{FolderPath}", folderPath);
                return StatusCode(500, "Failed to delete directory");
            }
        }

        [HttpDelete("{fileName}")]
        public async Task<IActionResult> DeleteFile(string fileName)
        {
            var filePath = Path.Combine(_uploadPath, fileName);
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("文件删除失败：文件不存在，文件名：{FileName}", fileName);
                return NotFound();
            }

            System.IO.File.Delete(filePath);
            _logger.LogInformation("文件删除成功：{FileName}", fileName);
            return Ok();
        }
    }
}