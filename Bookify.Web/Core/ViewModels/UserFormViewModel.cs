﻿using Microsoft.AspNetCore.Mvc.Rendering;
using UoN.ExpressiveAnnotations.NetCore.Attributes;

namespace Bookify.Web.Core.ViewModels
{
    public class UserFormViewModel
    {
        public string? Id { get; set; }
        [MaxLength(100, ErrorMessage = Errors.MaxLength), Display(Name = "Full Name"),
            RegularExpression(RegexPatterns.CharactersOnly_Eng, ErrorMessage = Errors.OnlyEnglishLetters)]
        public string FullName { get; set; } = null!;
        [MaxLength(20, ErrorMessage = Errors.MaxLength), Display(Name = "User Name")]
        [Remote("AllowItemByName", null!, AdditionalFields = "Id", ErrorMessage = Errors.Duplicated),
            RegularExpression(RegexPatterns.Username, ErrorMessage = Errors.InvalidUsername)]
        public string UserName { get; set; } = null!;
        [EmailAddress]
        [Display(Name = "Email")]
        [Remote("AllowItemByEmail", null!, AdditionalFields = "Id", ErrorMessage = Errors.Duplicated)]
        public string Email { get; set; } = null!;
        [StringLength(100, ErrorMessage = Errors.MaxMinLength, MinimumLength = 8),
            RegularExpression(RegexPatterns.Password, ErrorMessage = Errors.WeakPassword)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [RequiredIf("Id == null", ErrorMessage = Errors.RequiredField)]
        public string? Password { get; set; } = null!;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = Errors.Confirm)]
        [RequiredIf("Id == null", ErrorMessage = Errors.RequiredField)]
        public string? ConfirmPassword { get; set; } = null!;

        [Display(Name = "Roles")]
        public IList<string> SelectedRoles { get; set; } = new List<string>();
        public IEnumerable<SelectListItem>? Roles { get; set; }
    }
}
