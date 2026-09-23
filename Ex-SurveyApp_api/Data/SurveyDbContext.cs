using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Data;

public class SurveyDbContext(DbContextOptions<SurveyDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RegistrationOtp> RegistrationOtps => Set<RegistrationOtp>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistQuestion> ChecklistQuestions => Set<ChecklistQuestion>();
    public DbSet<SurveySession> SurveySessions => Set<SurveySession>();
    public DbSet<SurveyAnswer> SurveyAnswers => Set<SurveyAnswer>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<SurveyFile> Files => Set<SurveyFile>();
    public DbSet<QcReview> QcReviews => Set<QcReview>();
    public DbSet<ReportRevision> ReportRevisions => Set<ReportRevision>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncBatch> SyncBatches => Set<SyncBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<User>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ApprovalReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistrationOtp>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.Email).HasMaxLength(320);
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.DisplayName).HasMaxLength(200);
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.Otp).HasMaxLength(4).IsFixedLength();
        modelBuilder.Entity<Job>().HasIndex(x => x.JobNumber).IsUnique();
        modelBuilder.Entity<Job>().HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JobAssignment>().HasIndex(x => new { x.JobId, x.UserId, x.Role }).IsUnique();
        modelBuilder.Entity<JobAssignment>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        modelBuilder.Entity<JobAssignment>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Area>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        modelBuilder.Entity<Equipment>().HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId);
        modelBuilder.Entity<Equipment>().HasIndex(x => new { x.AreaId, x.TagNumber }).IsUnique();
        modelBuilder.Entity<ChecklistQuestion>().HasOne<ChecklistTemplate>().WithMany().HasForeignKey(x => x.ChecklistTemplateId);
        modelBuilder.Entity<SurveySession>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<User>().WithMany().HasForeignKey(x => x.SurveyorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<ChecklistTemplate>().WithMany().HasForeignKey(x => x.ChecklistTemplateId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyAnswer>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId);
        modelBuilder.Entity<SurveyAnswer>().HasOne<ChecklistQuestion>().WithMany().HasForeignKey(x => x.ChecklistQuestionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyAnswer>().HasIndex(x => new { x.SurveySessionId, x.ChecklistQuestionId }).IsUnique();
        modelBuilder.Entity<Finding>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId);
        modelBuilder.Entity<QcReview>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId);
        modelBuilder.Entity<QcReview>().HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<Finding>().WithMany().HasForeignKey(x => x.FindingId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<User>().WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReportRevision>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        modelBuilder.Entity<ReportRevision>().HasOne<SurveyFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReportRevision>().HasIndex(x => new { x.JobId, x.Revision }).IsUnique();
        modelBuilder.Entity<AuditLog>().HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SyncBatch>().HasOne<User>().WithMany().HasForeignKey(x => x.SurveyorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SyncBatch>().HasIndex(x => new { x.SurveyorId, x.ClientBatchId }).IsUnique();
    }
}
