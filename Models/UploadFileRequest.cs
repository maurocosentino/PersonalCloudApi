using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
namespace PersonalCloudApi.Models
{

    //public class UploadFileRequest
    //{
    //    [Required(ErrorMessage = "El archivo es obligatorio")]
    //    public IFormFile File { get; set; }

    //    [Display(Name = "Carpeta (opcional)")]
    //    public string? Folder { get; set; }
    //}
    public class UploadFileRequest
    {
        public IFormFile? File { get; set; }
        public string? Folder { get; set; }
    }


}
