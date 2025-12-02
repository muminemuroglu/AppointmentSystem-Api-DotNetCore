using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using RestApi.Dto.AppointmentDto;
using RestApi.Models;
using RestApi.Utils;

namespace RestApi.Services
{
    public class AppointmentService
    {

        private readonly ApplicationDbContext _dbContext;
        private readonly IMapper _mapper;
         TimeZoneInfo turkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");//randevu önerilerinde saati doğru formatta döndürmek için kullanıyoruz.
        public AppointmentService(ApplicationDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }


       //Bu metot, kullanıcının istediği saatte randevu oluşturmayı dener ve çakışma olursa alternatif saatler önerir.
        public object Add(AppointmentAddDto appointmentAddDto, string? userId)
        {
            long userIdValue = AppointmentDataControl( appointmentAddDto, userId);
            var appointment = _mapper.Map<Appointment>(appointmentAddDto);
            appointment.UserId = Convert.ToInt64(userId);//Gelen string userID yi bizim yapımızdaki gibi longa çeviriyoruz.
            var serviceTime = _dbContext.Services.FirstOrDefault(item => item.Sid == appointmentAddDto.ServiceId)?.DurationMinute; //İlgili hizmetin süresini database den alıyoruz.
           
            if (serviceTime == null)
                return "Servis süresi bulunamadı.";

            var serviceDuration = Convert.ToDouble(serviceTime);
            var appointDate = appointment.AppointmentDate;
            var addServiceTimeAppointDate = appointDate.AddMinutes(serviceDuration);//randevu başlangıç + hizmet süresi = randevu bitiş zamanı

            //Yeni randevunun başlangıcı ile mevcut randevunun bitişi, ve yeni randevunun bitişi ile mevcut randevunun başlangıcı arasında bir kesişme olup olmadığı kontrol edilir

            var timeControl = _dbContext.Appointments
                 .Include(a => a.Service)
                 .FirstOrDefault(
                     item =>
                         item.StaffId == appointment.StaffId &&
                         appointDate < item.AppointmentDate.AddMinutes(item.Service.DurationMinute) && // NewStart < ExistingEnd
                         addServiceTimeAppointDate > item.AppointmentDate // NewEnd > ExistingStart
                 );

            if (timeControl != null)
            {
                // çakışma varsa alternatif saatleri bul
                var suggestions = FindAvailableSlots(//FindAvailableSlots metodu çağrılarak alternatif uygun saatler aranır
                    appointment.StaffId,
                    serviceDuration,
                    appointment.AppointmentDate
                );

            
                return new
                {
                    Success = false,
                    Message = "İstediğiniz saat dolu. Alternatif saatler önerildi.",
                    SuggestedSlots = suggestions
                };
            }

            _dbContext.Appointments.Add(appointment);
            _dbContext.SaveChanges();

            // randevu işlemi başarılı ise
            return new
            {
                Success = true,
                Message = "Randevu başarıyla oluşturuldu.",
                Appointment = appointment
            };
        }

        //FindAvailableSlots fonk. ile kullanıcının tercih ettiği personel (staffId) için uygun randevu saatlerini arıyoruz.
        //Bir randevu dolu olduğunda kullanıcıya önümüzdeki günlerdeki uygun saatleri bulur. En fazla 5 öneri döndürür.
        //serviceDuration --> istenen hizmetin süresi
        //requestedDate --> müşterinin istediği tarih
        
        private List<string> FindAvailableSlots(long staffId, double serviceDuration, DateTime requestedDate)
        {
            var suggestions = new List<string>();//önerilen uygun saatlerin tutulacağı liste
            var workingDayStart = new TimeSpan(9, 0, 0);  // Sabah 09:00
            var workingDayEnd = new TimeSpan(18, 0, 0);   // Akşam 18:00
            var slotCheckInterval = 30; // 30 dakikalık aralıklarla kontrol et (örn: 09:00, 09:30, 10:00)
            var daySearchRange = new List<int> { 0, -1, 1, -2, 2 }; // Önce aynı gün, sonra +/- 1 gün, sonra +/- 2 gün  !!ancak güncel tarihten öncesi olamaz

            foreach (var dayOffset in daySearchRange)
            {
                
                if (suggestions.Count >= 5) break; // 5 öneri bulduysak döngüden çık

                var searchDay = requestedDate.Date.AddDays(dayOffset);

                // Geçmiş günleri ve Pazar günlerini atla 
                if (searchDay < DateTime.Today) continue;
                if (searchDay.DayOfWeek == DayOfWeek.Sunday) continue;

                // O personelin o günkü tüm randevularını ve bitiş saatlerini 1 defa çekip hafızaya al.
                var existingAppointments = _dbContext.Appointments
                    .Include(a => a.Service)
                    .Where(a =>
                        a.StaffId == staffId &&
                        a.AppointmentDate.Date == searchDay)
                    .Select(a => new
                    {
                        Start = a.AppointmentDate,
                        End = a.AppointmentDate.AddMinutes(a.Service.DurationMinute)
                    })
                    .ToList(); // Veritabanından veriyi çek ve hafızada tut

                // O günün slotlarını kontrol etmeye başla
                var potentialSlot = searchDay.Add(workingDayStart);

                // Eğer aradığımız gün "bugün" ise, geçmiş saatleri atla
                if (searchDay == DateTime.Today && potentialSlot < DateTime.Now)
                {
                    var now = DateTime.Now;
                    // Bir sonraki uygun "slotCheckInterval" aralığına yuvarla
                    var minutesToNextSlot = slotCheckInterval - (now.Minute % slotCheckInterval);
                    potentialSlot = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(minutesToNextSlot);
                }

                // Günün sonuna kadar randevu sıralarını tara
                while (potentialSlot.AddMinutes(serviceDuration) <= searchDay.Add(workingDayEnd))
                {
                    if (suggestions.Count >= 5) break;

                    var slotStart = potentialSlot;
                    var slotEnd = potentialSlot.AddMinutes(serviceDuration);

                    // HAFIZADAKİ LİSTE ile çakışma kontrolü yap (DB'ye tekrar gitmeye gerek yok)
                    var isSlotBusy = existingAppointments.Any(a =>
                        slotStart < a.End && // NewStart < ExistingEnd
                        slotEnd > a.Start    // NewEnd > ExistingStart
                    );

                    if (!isSlotBusy)
                    {
                        // Türkiye saat dilimine çevir ve ISO 8601 +03:00 formatında string yap
                        var turkeyTime = TimeZoneInfo.ConvertTime(slotStart, turkeyTimeZone);
                        var slotString = new DateTimeOffset(turkeyTime, turkeyTimeZone.GetUtcOffset(turkeyTime))
                                        .ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
                        suggestions.Add(slotString);
                    }

                    // Bir sonraki potansiyel slota geç
                    potentialSlot = potentialSlot.AddMinutes(slotCheckInterval);
                }
            }
            return suggestions;
        }


        // Randevu ekleme işlemi için gerekli kontrolleri yapar(Userıd geçerli mi ,istenilen uzman var mı, uzman kendisi mi?)
        private long AppointmentDataControl(AppointmentAddDto appointmentAddDto, string? userId)
        {
            // userId değer olarak long mu?
            if (string.IsNullOrEmpty(userId) || !long.TryParse(userId, out var userIdValue))
                throw new BadHttpRequestException("Kullanıcı ID geçersiz!");

            
            // uzman kontolü
            var staffUser = _dbContext.Users.FirstOrDefault(item =>
                item.Role.Contains("Staff") && item.Id == appointmentAddDto.StaffId
            );
            if (staffUser == null)
                throw new BadHttpRequestException("Böyle bir uzman yok!");
            
            // uzman kendisi mi?    
            if (staffUser.Id == userIdValue)
                throw new BadHttpRequestException("Kendinize randevu alamazsınız!");
            return userIdValue;
        }

          public object ListUpcomingByStaff(string? userId)
        {
            if (string.IsNullOrEmpty(userId) || !long.TryParse(userId, out var staffId))
                throw new BadHttpRequestException("Kullanıcı ID geçersiz!");

            var now = DateTime.Now;  //Şimdiki zamanı alıyoruz,soruda sadece gelecekteki randevuları gösterebilmek için
            var appointments = _dbContext.Appointments//Veritabanındaki randevular tablosuna erişim
            .Include(a => a.Service)//Her randevu için ilişkili Service (hizmet) ve User (kullanıcı) verilerini de sorguya ekliyor
            .Include(a => a.User)
            .Where(a => a.StaffId == staffId && a.AppointmentDate > now)//StaffId verilen staffId ile aynı olmalı (personelin randevuları)..AppointmentDate (randevu zamanı) now'dan sonra olmalı (gelecek randevular).
            .OrderBy(a => a.AppointmentDate)//(en yakın tarihten en uzak tarihe) sıralar
            .Select(a => new//Hangi alanlar dönecek?select ile bnu belirliyoruz("a"= randevu)
            {
                a.Aid,
                a.AppointmentDate,
                a.Status,
                Service = new
                {
                    a.Service.Sid,
                    a.Service.Name,
                    a.Service.Detail,
                    a.Service.DurationMinute,
                    a.Service.Price
                },
                User = new
                {
                    a.User.Id,
                    a.User.FirstName,
                    a.User.LastName,
                    a.User.Email
                }
                //NOT: Gereksiz tüm veri sütunlarını döndürüp ağ/performans israfı yapmak yerine sadece ihtiyaç olan alanlar dönülüyor.
            })
            .ToList();                //LINQ sorgusunu veritabanına gönderir, sonucu çekip belleğe (RAM) bir List olarak getirir.
            return appointments;     //Ne yapıyor: Oluşturulan anonim nesneler listesini metodu çağırana geri gönderir
        }

        public object AppointmentChangeStatus(string? userId, AppointmentStatusChangeDto appointmentStatusChangeDto)
        {
            if (string.IsNullOrEmpty(userId) || !long.TryParse(userId, out var staffId))
                throw new BadHttpRequestException("Kullanıcı ID geçersiz!");

            var appointment = _dbContext.Appointments.FirstOrDefault(item =>
                item.StaffId == staffId && //Giriş yapan kullanıcı olan staffın staff bilgisi gelsin
                item.Aid == appointmentStatusChangeDto.AppointmentId
            ) ?? throw new BadHttpRequestException("Kullanıcı ID geçersiz yada Yetki hatası!");

            appointment.Status = appointmentStatusChangeDto.Status;
            _dbContext.SaveChanges();

            return appointment;
        }

        public object AppointmentUserList(string? userId)
        {
            if (string.IsNullOrEmpty(userId) || !long.TryParse(userId, out var userID))
                throw new BadHttpRequestException("Kullanıcı ID geçersiz!");

            var now = DateTime.Now;
            var appointments = _dbContext.Appointments
            .Include(a => a.Service)
            .Include(a => a.Staff)
            .Where(a => a.UserId == userID && a.AppointmentDate > now)
            .OrderBy(a => a.AppointmentDate)
            .Select(a => new
            {
                a.Aid,
                a.AppointmentDate,
                a.Status,
                Service = new
                {
                    a.Service.Sid,
                    a.Service.Name,
                    a.Service.Detail,
                    a.Service.DurationMinute,
                    a.Service.Price
                },
                Staff = new
                {
                    a.Staff.Id,
                    a.Staff.FirstName,
                    a.Staff.LastName,
                    a.Staff.Email,
                }
            })
            .ToList();
            if (appointments.Count == 0)//Boş dizi dönmesi halinde json formatında mesaj döndürüyoruz.
            {
                return new { Message = "Güncel Randevunuz Yok!" };//Hata değil mesaj vermek istediğimiz için bu yapıyı kullandık.
            }
            return appointments;
        }
        
}
}


    
