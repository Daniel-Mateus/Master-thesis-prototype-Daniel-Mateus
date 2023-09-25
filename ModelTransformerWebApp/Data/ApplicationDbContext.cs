using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ModelTransformerWebApp.Models;

namespace ModelTransformerWebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<ModelTransformerWebApp.Models.Node> Node { get; set; } = default!;
        public DbSet<ModelTransformerWebApp.Models.Pattern> Pattern { get; set; } = default!;
        public DbSet<ModelTransformerWebApp.Models.BpmnExtraction> BpmnExtraction { get; set; } = default!;
    }
}