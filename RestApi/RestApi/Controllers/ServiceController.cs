using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestApi.Dto.ServiceDto;
using RestApi.Models;
using RestApi.Services;
using System.Collections.Generic;
using System.Linq;

namespace RestApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceController : ControllerBase
    {

        private readonly CategoryService _categoryService;
        public ServiceController(CategoryService categoryService)//Bu bir injecte yöntemidir.Program ayağa kalkarken categoryService i injekte ediyoruz
        {
            _categoryService = categoryService;
        }

        [Authorize(Roles = "Admin")]//Bu sayede sadece admin hizmet ekleyebilecek.
        [HttpPost("add")] //Dışardan data alacağımız için post işlemi yapıyoruz
        public IActionResult Add(ServiceAddDto serviceAddDto)
        {
            var addService = _categoryService.ServiceAdd(serviceAddDto);
            if (addService != null)
            {
                return Ok(addService);
            }
            return BadRequest(serviceAddDto.Name + " All Ready Use");
        }

       [HttpGet("all")]
        [Authorize]
        public List<Service> GetAllService()
        {
            return _categoryService.GetAllService();
        }

    }

}