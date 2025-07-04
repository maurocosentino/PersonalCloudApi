using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using PersonalCloudApi;
using PersonalCloudApi.Models;
using Swashbuckle.AspNetCore.Annotations;
using System.IO.Compression;


[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Archivos")]

public class FilesController : ControllerBase //ControllerBase (base para APIs sin vistas)
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
    [SwaggerOperation(Summary = "Crea una carpeta", Description = "Crea una subcarpeta en el almacenamiento raíz.")]
    [SwaggerResponse(200, "Carpeta creada")]
    [SwaggerResponse(400, "Nombre inválido")]
    [SwaggerResponse(409, "La carpeta ya existe")]
    public IActionResult CreateFolder([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains("..") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return BadRequest("Nombre de carpeta inválido.");

        var path = Path.Combine(_storagePath, name);

        if (Directory.Exists(path))
            return Conflict("La carpeta ya existe.");

        Directory.CreateDirectory(path);

        return Ok(new { mensaje = "Carpeta creada exitosamente", carpeta = name });
    }

    [HttpGet("folders")]
    [SwaggerOperation(Summary = "Lista carpetas", Description = "Devuelve los nombres de todas las subcarpetas en la raíz.")]
    [SwaggerResponse(200, "Listado obtenido", typeof(IEnumerable<string>))]
    public IActionResult ListFolders()
    {
        if (!Directory.Exists(_storagePath))
            return Ok(Enumerable.Empty<string>());

        var folders = Directory.GetDirectories(_storagePath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

        return Ok(folders);
    }

    [HttpGet("list")]
    [SwaggerOperation(Summary = "Lista archivos", Description = "Lista archivos en la raíz o en una carpeta, con orden, filtros y paginación.")]
    [SwaggerResponse(200, "Listado paginado de archivos")]
    [SwaggerResponse(400, "Parámetros inválidos")]
    [SwaggerResponse(404, "Carpeta no encontrada")]
    public IActionResult ListFiles(
    [FromQuery] string? folder,
    [FromQuery] string? sortBy,
    [FromQuery] string? order,
    [FromQuery] string? mime,
    [FromQuery] string? ext,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 0 || pageSize > 1000)
            return BadRequest("Parámetros de paginación inválidos.");

        string path = _storagePath;

        if (!string.IsNullOrWhiteSpace(folder))
        {
            if (folder.Contains("..") || folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return BadRequest("Nombre de carpeta inválido.");

            path = Path.Combine(_storagePath, folder);
            if (!Directory.Exists(path))
                return NotFound("Carpeta no encontrada.");
        }

        if (!Directory.Exists(path))
            return Ok(new
            {
                totalArchivos = 0,
                archivos = Enumerable.Empty<object>()
            });

        var archivos = Directory.GetFiles(path)
            .Select(file =>
            {
                var fileInfo = new FileInfo(file);
                return new ArchivoDto
                {
                    Nombre = fileInfo.Name,
                    Tamaño = fileInfo.Length,
                    FechaSubida = fileInfo.CreationTime,
                    TipoMime = MimeTypes.GetMimeType(fileInfo.Extension),
                    Carpeta = string.IsNullOrWhiteSpace(folder) ? null : folder,
                    Url = GenerateUrl(
                        string.IsNullOrWhiteSpace(folder)
                            ? fileInfo.Name
                            : Path.Combine(folder, fileInfo.Name))
                };
            });

        if (!string.IsNullOrWhiteSpace(mime))
            archivos = archivos.Where(a => a.TipoMime.Equals(mime, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(ext))
        {
            var normalizedExt = ext.StartsWith(".") ? ext : "." + ext;
            archivos = archivos.Where(a => Path.GetExtension(a.Nombre).Equals(normalizedExt, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            bool descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
            archivos = sortBy.ToLower() switch
            {
                "fecha" => descending ? archivos.OrderByDescending(a => a.FechaSubida) : archivos.OrderBy(a => a.FechaSubida),
                "tamaño" => descending ? archivos.OrderByDescending(a => a.Tamaño) : archivos.OrderBy(a => a.Tamaño),
                "nombre" => descending ? archivos.OrderByDescending(a => a.Nombre) : archivos.OrderBy(a => a.Nombre),
                _ => archivos
            };
        }

        int total = archivos.Count();

        // Si pageSize == 0, devolver todos los archivos sin paginar
        if (pageSize == 0)
        {
            return Ok(new
            {
                totalArchivos = total,
                archivos = archivos.ToList()
            });
        }

        // Aplicar paginación
        int totalPaginas = (int)Math.Ceiling((double)total / pageSize);
        var archivosPagina = archivos
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new
        {
            paginaActual = page,
            totalPaginas,
            totalArchivos = total,
            archivos = archivosPagina
        });
    }

    [HttpGet("list-all")]
    [SwaggerOperation(Summary = "Lista todos los archivos", Description = "Incluye archivos en raíz y subcarpetas.")]
    [SwaggerResponse(200, "Listado completo de archivos", typeof(IEnumerable<object>))]
    public IActionResult ListAllFiles()
    {
        var archivos = Directory.GetFiles(_storagePath, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var fileInfo = new FileInfo(path);
                var relativeDir = Path.GetRelativePath(_storagePath, fileInfo.DirectoryName!);
                var relativePath = Path.GetRelativePath(_storagePath, path);

                return new ArchivoDto
                {
                    Nombre = fileInfo.Name,
                    Tamaño = fileInfo.Length,
                    FechaSubida = fileInfo.CreationTime,
                    TipoMime = MimeTypes.GetMimeType(fileInfo.Extension),
                    Carpeta = string.IsNullOrWhiteSpace(relativeDir) || relativeDir == "." ? null : relativeDir,
                    Url = GenerateUrl(relativePath)
                };
            })
            .ToList();

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
    private string GenerateUrl(string relativePath)
    {
        var encodedPath = relativePath.Replace("\\", "/");
        return $"{Request.Scheme}://{Request.Host}/Archivos/{encodedPath}";
    }
}
