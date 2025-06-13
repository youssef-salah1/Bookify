using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasSequence<int>("SerialNumber", schema: "shared").StartsAt(10001);
            builder.Entity<BookCopy>().Property(p => p.SerialNumber).HasDefaultValueSql("NEXT VALUE FOR shared.SerialNumber");
            builder.Entity<BookCategory>().HasKey(e => new { e.BookId, e.CategoryId });
            base.OnModelCreating(builder);
        }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Author> BookCategories { get; set; }
        public DbSet<BookCopy> BookCopies { get; set; }
    }
}
