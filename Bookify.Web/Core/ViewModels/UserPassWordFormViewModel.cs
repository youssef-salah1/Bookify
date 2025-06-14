namespace Bookify.Web.Core.ViewModels
{
    public class UserPassWordFormViewModel
    {
        public string Id { get; set; } = null!;
        [StringLength(100, ErrorMessage = Errors.MaxMinLength, MinimumLength = 8),
            RegularExpression(RegexPatterns.Password, ErrorMessage = Errors.WeakPassword)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = null!;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = Errors.Confirm)]
        public string ConfirmPassword { get; set; } = null!;
    }
}
