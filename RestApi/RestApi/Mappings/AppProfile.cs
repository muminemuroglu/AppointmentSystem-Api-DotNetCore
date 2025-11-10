
using AutoMapper;
using RestApi.Models;
using RestApi.Dto.UserDto;
using RestApi.Dto.ServiceDto;
using RestApi.Dto.AppointmentDto;

namespace RestApi.Mappings
{
    public class AppProfile : Profile
    {
        public AppProfile()
        {

            //USER
            CreateMap<UserRegisterDto, User>();// DTO'umuz Entity'e dönüşüyor burada
            CreateMap<UserLoginDto, User>();//Cerate Map'ın içerinde dto yolluyoruz ve buradan geriye user bekliyorum demektir
            CreateMap<User, UserJwtDto>();

            //SERVİCE
            CreateMap<ServiceAddDto, Service>();

            //APPOINTMENT
             CreateMap<AppointmentAddDto, Appointment>();
        }
    }
}
