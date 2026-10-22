using Microsoft.EntityFrameworkCore;
using PayrollSystem.Models;

namespace PayrollSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<BackupPayroll> BackupPayroll { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<Payroll> Payroll { get; set; }
        public DbSet<DeActivate> DeActivate { get; set; }
        
        //public DbSet<Payroll> Payroll { get; set; }
        //public DbSet<DeActivate> DeActivate { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tell EF Core to treat the database columns as decimal(18,2)
            var payrollEntity = modelBuilder.Entity<Payroll>();

            // Earnings/Allowances
            payrollEntity.Property(p => p.Consolidated).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.Allowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.TeachingAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.CallDutyAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.HazardAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.ShiftAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.SpecialistAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.RuralPostingAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.AccoutrementAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.ConmessClinicalDutyAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.ConmessNonClinicalAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.PharmacistConsultAllowance).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.Arrears).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.Refund).HasColumnType("decimal(18,2)");

            // Deductions
            payrollEntity.Property(p => p.OverPaymentDeduction).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.Pension).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.Tax).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.TAX_ARREARS).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.AMLSN).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.NMA_DUES).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.NMA_SPECIAL_LEVY).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.MEDCAN_DUES).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.ARD).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.NARD).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.RENT).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.RENT_ARREARS).HasColumnType("decimal(18,2)");

            // Calculated Values
            payrollEntity.Property(p => p.Gross_Pay).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.Deductions).HasColumnType("decimal(18,2)");
            payrollEntity.Property(p => p.Net_Pay).HasColumnType("decimal(18,2)");
        }
    }
}