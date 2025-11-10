using Microsoft.AspNetCore.Mvc;
using System;
using AutoMapper;
using RestApi.Utils;
using RestApi.Dto.ServiceDto;
using RestApi.Models;

namespace RestApi.Services
{
    public class CategoryService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMapper _mapper;
        public CategoryService(ApplicationDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        public Service? ServiceAdd(ServiceAddDto serviceAddDto)
        {
            var Service = _mapper.Map<Service>(serviceAddDto);
            var ServiceDb = _dbContext.Services.FirstOrDefault(item => item.Name == serviceAddDto.Name);//Veritabanında benim gönderdiğim name karşılık gelen bir nasme var mı.Bu varlık denetiminden sonra ya nul yada ServiceDb gelir sonuç olarak.
            if (ServiceDb == null)
            {
                //Eğer veritanında name null ise yani o kategori yoksa ekle poziyonuna geçiyoruz.
                _dbContext.Add(Service);
                _dbContext.SaveChanges();
                return Service;
            }
            return null;
        }

        public List<Service> GetAllService()
        {
            return _dbContext.Services.ToList();
        }

    }
}