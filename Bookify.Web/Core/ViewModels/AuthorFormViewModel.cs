namespace Bookify.Web.Core.ViewModels
{
    public class AuthorFormViewModel
    {
        public int Id { get; set; }

        [MaxLength(100, ErrorMessage = "Max length cannot be more than 100 chr."),
            RegularExpression(RegexPatterns.CharactersOnly_Eng, ErrorMessage = Errors.OnlyEnglishLetters)]
        [Remote("AllowItem", null!, AdditionalFields = "Id", ErrorMessage = "Category with the same name is already exists!")]
        public string Name { get; set; } = null!;
    }
}
