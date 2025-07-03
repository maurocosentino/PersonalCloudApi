using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using PersonalCloudApi.Models;
using Swashbuckle.AspNetCore.Annotations;
using System.IO.Compression;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Archivos")]
public class FilesController : ControllerBase
{
    private readonly string _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Archivos");

    public FilesController()
    {
        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(Summary = "Sube un archivo", Description = "Permite subir un archivo a raíz o a una carpeta específica.")]
    [SwaggerResponse(200, "Archivo subido exitosamente")]
    [SwaggerResponse(400, "Archivo o carpeta inválida")]
    public IActionResult Upload([FromForm] UploadFileRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("Archivo no válido.");

        var folder = request.Folder?.Trim();
        if (folder?.Contains("..") == true)
            return BadRequest("Nombre de carpeta inválido.");

        var targetPath = string.IsNullOrWhiteSpace(folder)
            ? _storagePath
            : Path.Combine(_storagePath, folder);

        if (!Directory.Exists(targetPath))
            Directory.CreateDirectory(targetPath);

        var filePath = Path.Combine(targetPath, request.File.FileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        request.File.CopyTo(stream);

        return Ok(new
        {
            mensaje = "Archivo subido exitosamente",
            nombre = request.File.FileName,
            carpeta = string.IsNullOrWhiteSpace(folder) ? "(raíz)" : folder
        });
    }

    [HttpPost("create-folder")]
    [SwaggerOperation(Summary = "Crea una carpeta", Description = "Crea una carpeta vacía en el almacenamiento.")]
    [SwaggerResponse(200, "Carpeta creada")]
    [SwaggerResponse(400, "Nombre inválido")]
    [SwaggerResponse(409, "La carpeta ya existe")]
    public IActionResult CreateFolder([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains(".."))
            return BadRequest("Nombre inválido");

        var path = Path.Combine(_storagePath, name);
        if (Directory.Exists(path))
            return Conflict("La carpeta ya existe");

        Directory.CreateDirectory(path);
        return Ok(new { mensaje = "Carpeta creada" });
    }

    [HttpGet("folders")]
    [SwaggerOperation(Summary = "Lista carpetas", Description = "Devuelve los nombres de todas las subcarpetas.")]
    [SwaggerResponse(200, "Listado obtenido", typeof(IEnumerable<string>))]
    public IActionResult ListFolders()
    {
        var folders = Directory.Exists(_storagePath)
            ? Directory.GetDirectories(_storagePath).Select(Path.GetFileName)
            : Enumerable.Empty<string>();

        return Ok(folders);
    }

    [HttpGet("list-folder")]
    [SwaggerOperation(Summary = "Lista archivos de carpeta", Description = "Devuelve archivos de una carpeta específica.")]
    [SwaggerResponse(200, "Listado de archivos", typeof(IEnumerable<object>))]
    [SwaggerResponse(400, "Carpeta inválida")]
    [SwaggerResponse(404, "Carpeta no encontrada")]
    public IActionResult ListFilesInFolder([FromQuery] string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || folder.Contains(".."))
            return BadRequest("Debe indicar una carpeta válida.");

        var path = Path.Combine(_storagePath, folder);
        if (!Directory.Exists(path))
            return NotFound("Carpeta no encontrada");

        var archivos = Directory.GetFiles(path)
            .Select(file => new
            {
                nombre = Path.GetFileName(file),
                tamaño = new FileInfo(file).Length,
                fechaSubida = System.IO.File.GetCreationTime(file),
                tipoMime = MimeTypes.GetMimeType(file),
                carpeta = folder,
                url = $"{Request.Scheme}://{Request.Host}/Archivos/{folder}/{Path.GetFileName(file)}"
            });

        return Ok(archivos);
    }

    [HttpGet("list-root")]
    [SwaggerOperation(Summary = "Lista archivos en raíz", Description = "Devuelve los archivos sueltos (no en carpetas).")]
    [SwaggerResponse(200, "Listado de archivos en raíz", typeof(IEnumerable<object>))]
    public IActionResult ListRootFiles()
    {
        var files = Directory.Exists(_storagePath)
            ? Directory.GetFiles(_storagePath).Select(file => new
            {
                nombre = Path.GetFileName(file),
                tamaño = new FileInfo(file).Length,
                fechaSubida = System.IO.File.GetCreationTime(file),
                tipoMime = MimeTypes.GetMimeType(file),
                carpeta = "(raíz)",
                url = $"{Request.Scheme}://{Request.Host}/Archivos/{Path.GetFileName(file)}"
            })
            : Enumerable.Empty<object>();

        return Ok(files);
    }

    [HttpGet("list-all")]
    [SwaggerOperation(Summary = "Lista todos los archivos", Description = "Incluye archivos en raíz y subcarpetas.")]
    [SwaggerResponse(200, "Listado completo de archivos", typeof(IEnumerable<object>))]
    public IActionResult ListAllFiles()
    {
        var archivos = Directory.Exists(_storagePath)
            ? Directory.GetFiles(_storagePath, "*.*", SearchOption.AllDirectories).Select(file =>
            {
                var relativePath = Path.GetRelativePath(_storagePath, Path.GetDirectoryName(file));
                var carpeta = string.IsNullOrWhiteSpace(relativePath) || relativePath == "." ? null : relativePath.Replace("\\", "/");

                return new
                {
                    nombre = Path.GetFileName(file),
                    tamaño = new FileInfo(file).Length,
                    fechaSubida = System.IO.File.GetCreationTime(file),
                    tipoMime = MimeTypes.GetMimeType(file),
                    carpeta,
                    url = $"{Request.Scheme}://{Request.Host}/Archivos/{(string.IsNullOrWhiteSpace(relativePath) || relativePath == "." ? "" : relativePath + "/")}{Path.GetFileName(file)}"
                };
            })
            : Enumerable.Empty<object>();

        return Ok(archivos);
    }

    [HttpGet("download-zip")]
    [SwaggerOperation(Summary = "Descarga carpeta como ZIP", Description = "Genera un .zip de la carpeta indicada y lo descarga.")]
    [SwaggerResponse(200, "Archivo zip generado")]
    [SwaggerResponse(400, "Carpeta inválida")]
    [SwaggerResponse(404, "Carpeta no encontrada")]
    public IActionResult DownloadZip([FromQuery] string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || folder.Contains(".."))
            return BadRequest("Debe indicar una carpeta válida.");

        var targetPath = Path.Combine(_storagePath, folder);
        if (!Directory.Exists(targetPath))
            return NotFound("Carpeta no encontrada");

        var tempZip = Path.Combine(Path.GetTempPath(), $"Archivos_{folder}_{Guid.NewGuid()}.zip");
        ZipFile.CreateFromDirectory(targetPath, tempZip);

        var zipName = $"Archivos_{folder}.zip";
        return PhysicalFile(tempZip, "application/zip", zipName);
    }

    [HttpDelete("delete-file")]
    [SwaggerOperation(Summary = "Elimina un archivo", Description = "Requiere nombre de archivo y carpeta (o raíz).")]
    [SwaggerResponse(200, "Archivo eliminado")]
    [SwaggerResponse(400, "Parámetros inválidos")]
    [SwaggerResponse(404, "Archivo no encontrado")]
    public IActionResult DeleteFile([FromQuery] string nombre, [FromQuery] string? carpeta)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest("Nombre no especificado");

        if ((carpeta ?? "").Contains("..") || nombre.Contains(".."))
            return BadRequest("Ruta inválida.");

        var isRoot = string.IsNullOrWhiteSpace(carpeta) || carpeta == "." || carpeta == "null";
        var fullPath = isRoot
            ? Path.Combine(_storagePath, nombre)
            : Path.Combine(_storagePath, carpeta!, nombre);

        if (!System.IO.File.Exists(fullPath))
            return NotFound($"Archivo no encontrado: {fullPath}");

        System.IO.File.Delete(fullPath);
        return Ok(new { mensaje = "Archivo eliminado correctamente" });
    }

    [HttpDelete("delete-folder")]
    [SwaggerOperation(Summary = "Elimina una carpeta", Description = "Borra recursivamente toda la carpeta y su contenido.")]
    [SwaggerResponse(200, "Carpeta eliminada")]
    [SwaggerResponse(400, "Carpeta inválida")]
    [SwaggerResponse(404, "Carpeta no encontrada")]
    public IActionResult DeleteFolder([FromQuery] string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || folder.Contains(".."))
            return BadRequest("Carpeta inválida");

        var fullPath = Path.Combine(_storagePath, folder);
        if (!Directory.Exists(fullPath))
            return NotFound("Carpeta no encontrada");

        Directory.Delete(fullPath, true);
        return Ok(new { mensaje = "Carpeta eliminada correctamente" });
    }
}

//public static class MimeTypes
//{
//    public static string GetMimeType(string fileName)
//    {
//        return new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider()
//            .TryGetContentType(fileName, out var mime)
//            ? mime
//            : "application/octet-stream";
//    }
//}
