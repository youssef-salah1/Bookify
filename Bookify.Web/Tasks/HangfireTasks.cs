﻿using Bookify.Web.Services;
using Cover_to_Cover.Web.Core.Consts;
using Microsoft.AspNetCore.Identity.UI.Services;


namespace Bookify.Web.Tasks
{
    public class HangfireTasks
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        private readonly IEmailBodyBuilder _emailBodyBuilder;
        private readonly IEmailSender _emailSender;

        public HangfireTasks(ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            IEmailBodyBuilder emailBodyBuilder,
            IEmailSender emailSender)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _emailBodyBuilder = emailBodyBuilder;
            _emailSender = emailSender;
        }
        public async Task PrepareExpirationAlert()
        {
            var subscribers = _context.Subscribers
                .Include(s => s.Subscriptions)
                .Where(s => !s.IsBlackListed && s.Subscriptions.OrderByDescending(x => x.EndDate).First().EndDate == DateTime.Today.AddDays(5))
                .ToList();

            foreach (var subscriber in subscribers)
            {
                var endDate = subscriber.Subscriptions.Last().EndDate.ToString("d MMM, yyyy");

                //Send email and WhatsApp Message
                var placeholders = new Dictionary<string, string>()
                {
                    { "imageUrl", "https://res.cloudinary.com/devcreed/image/upload/v1671062674/calendar_zfohjc.png" },
                    { "header", $"Hello {subscriber.FirstName}," },
                    { "body", $"your subscription will be expired by {endDate} 🙁" }
                };

                var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);

                await _emailSender.SendEmailAsync(
                    subscriber.Email,
                    "Bookify Subscription Expiration", body);

                //if (subscriber.HasWhatsApp)
                //{
                //    var components = new List<WhatsAppComponent>()
                //    {
                //        new WhatsAppComponent
                //        {
                //            Type = "body",
                //            Parameters = new List<object>()
                //            {
                //                new WhatsAppTextParameter { Text = subscriber.FirstName },
                //                new WhatsAppTextParameter { Text = endDate },
                //            }
                //        }
                //    };

                //    var mobileNumber = _webHostEnvironment.IsDevelopment() ? "Add Your number" : subscriber.MobileNumber;

                //    //Change 2 with your country code
                //    await _whatsAppClient
                //        .SendMessage($"2{mobileNumber}", WhatsAppLanguageCode.English,
                //        WhatsAppTemplates.SubscriptionExpiration, components);
                //}
            }
        }

    }
}