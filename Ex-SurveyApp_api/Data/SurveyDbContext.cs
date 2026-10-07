using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Data;

public class SurveyDbContext(DbContextOptions<SurveyDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RegistrationOtp> RegistrationOtps => Set<RegistrationOtp>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectArea> ProjectAreas => Set<ProjectArea>();
    public DbSet<ProjectAreaPhoto> ProjectAreaPhotos => Set<ProjectAreaPhoto>();
    public DbSet<AreaEquipment> AreaEquipment => Set<AreaEquipment>();
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
        modelBuilder.Entity<User>().HasOne(x => x.Manager).WithMany()
            .HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AuditLog>().Property(x => x.Comments).HasMaxLength(4000);
        modelBuilder.Entity<AuditLog>().HasIndex(x => new { x.EntityId, x.OccurredAt });
        modelBuilder.Entity<User>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ApprovalReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistrationOtp>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.Email).HasMaxLength(320);
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.DisplayName).HasMaxLength(200);
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.Otp).HasMaxLength(4).IsFixedLength();
        modelBuilder.Entity<Project>().Property(x => x.ProjectName).HasMaxLength(200);
        modelBuilder.Entity<Project>().Property(x => x.Sector).HasMaxLength(150);
        modelBuilder.Entity<Project>().Property(x => x.JobNo).HasMaxLength(100);
        modelBuilder.Entity<Project>().Property(x => x.Location).HasMaxLength(300);
        modelBuilder.Entity<Project>().Property(x => x.VesselOrSiteName).HasMaxLength(200);
        modelBuilder.Entity<Project>().Property(x => x.HazardousAreaPlanPath).HasMaxLength(500);
        modelBuilder.Entity<Project>().Property(x => x.HazardousAreaPlanFileName).HasMaxLength(255);
        modelBuilder.Entity<Project>().HasIndex(x => x.JobNo).IsUnique();
        modelBuilder.Entity<Project>().HasOne<User>().WithMany().HasForeignKey(x => x.AssignedSurveyorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().HasOne(x => x.AssignedQc).WithMany().HasForeignKey(x => x.AssignedQcId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().HasOne<User>().WithMany().HasForeignKey(x => x.AssignedClientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProjectArea>().Property(x => x.AreaName).HasMaxLength(200);
        modelBuilder.Entity<ProjectArea>().Property(x => x.LayoutImagePath).HasMaxLength(500);
        modelBuilder.Entity<ProjectArea>().HasIndex(x => new { x.ProjectId, x.AreaName }).IsUnique();
        modelBuilder.Entity<ProjectArea>().HasOne<Project>().WithMany(x => x.Areas).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectAreaPhoto>().Property(x => x.FileName).HasMaxLength(255);
        modelBuilder.Entity<ProjectAreaPhoto>().Property(x => x.FilePath).HasMaxLength(500);
        modelBuilder.Entity<ProjectAreaPhoto>().HasOne<ProjectArea>().WithMany(x => x.Photos).HasForeignKey(x => x.ProjectAreaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectAreaPhoto>().HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.XPercent).HasPrecision(5, 2);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.YPercent).HasPrecision(5, 2);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.TagNumber).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.EquipmentCode).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.Type).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.CircuitId).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.Location).HasMaxLength(300);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.Manufacturer).HasMaxLength(200);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.ProtectionDetails).HasMaxLength(500);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.CertificateNumber).HasMaxLength(150);
        modelBuilder.Entity<AreaEquipment>().HasIndex(x => new { x.AreaId, x.SequenceNo }).IsUnique();
        modelBuilder.Entity<AreaEquipment>().HasIndex(x => new { x.AreaId, x.TagNumber }).IsUnique();
        modelBuilder.Entity<AreaEquipment>().HasOne<Project>().WithMany(x => x.EquipmentPins).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AreaEquipment>().HasOne<ProjectArea>().WithMany(x => x.EquipmentPins).HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AreaEquipment>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
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
        modelBuilder.Entity<SurveySession>().HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.AreaEquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<User>().WithMany().HasForeignKey(x => x.SurveyorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<ChecklistTemplate>().WithMany().HasForeignKey(x => x.ChecklistTemplateId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasIndex(x => x.AreaEquipmentId).IsUnique().HasFilter("[AreaEquipmentId] IS NOT NULL");
        modelBuilder.Entity<SurveySession>().Property(x => x.InspectorName).HasMaxLength(200);
        modelBuilder.Entity<SurveySession>().Property(x => x.SurveyorRemarks).HasMaxLength(2000);
        modelBuilder.Entity<SurveyAnswer>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId);
        modelBuilder.Entity<SurveyAnswer>().HasOne<ChecklistQuestion>().WithMany().HasForeignKey(x => x.ChecklistQuestionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyAnswer>().HasIndex(x => new { x.SurveySessionId, x.ChecklistQuestionId }).IsUnique();
        modelBuilder.Entity<Finding>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId);
        modelBuilder.Entity<QcReview>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId);
        modelBuilder.Entity<QcReview>().HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QcReview>().HasIndex(x => x.SurveySessionId).IsUnique();
        modelBuilder.Entity<QcReview>().Property(x => x.Comments).HasMaxLength(4000);
        modelBuilder.Entity<SurveyFile>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId).OnDelete(DeleteBehavior.Cascade);
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
