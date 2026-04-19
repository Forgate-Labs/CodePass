using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Data;

public sealed class CodePassDbContext(DbContextOptions<CodePassDbContext> options) : DbContext(options)
{
}
