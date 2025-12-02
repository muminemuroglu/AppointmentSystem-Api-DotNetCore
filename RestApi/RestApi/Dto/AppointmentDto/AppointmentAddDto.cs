using System;
using System.ComponentModel.DataAnnotations;
using RestApi.CustomValidations;


namespace RestApi.Dto.AppointmentDto
{
  

    public class AppointmentAddDto
    {

       [Required]
        [Range(minimum: 1, maximum: int.MaxValue)]
        public int ServiceId { get; set; }//Hangi hizmet için randevu alınyor?
        
        [Required]
        [Range(minimum: 1, maximum: long.MaxValue)]
        public long StaffId { get; set; }//Kimden randevu alıyor?

        [Required]
        [WorkingHoursFutureDate] //Kendi oluşturduğumuz notasyon-bugünden büyük ve 9-17 saatleri arasında olacak kuralı
        public DateTime AppointmentDate { get; set; }


    }
}