using Microsoft.EntityFrameworkCore;
using ReverseGantt.Models;

namespace ReverseGantt.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<Dependency> Dependencies => Set<Dependency>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskEvaluation> TaskEvaluations => Set<TaskEvaluation>();
    public DbSet<TaskResult> TaskResults => Set<TaskResult>();



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>().ToTable("Tasks");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Participant)
            .WithMany()
            .HasForeignKey(u => u.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Team>()
            .HasMany(t => t.Participants)
            .WithOne(p => p.Team)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Team>()
            .HasMany(t => t.Projects)
            .WithOne(p => p.Team)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.Tasks)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.Comments)
            .WithOne(c => c.Project)
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskAssignment>()
            .HasKey(x => new { x.TaskItemId, x.ParticipantId });

        modelBuilder.Entity<TaskAssignment>()
            .HasOne(x => x.TaskItem)
            .WithMany(t => t.Assignments)
            .HasForeignKey(x => x.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskAssignment>()
            .HasOne(x => x.Participant)
            .WithMany(p => p.Assignments)
            .HasForeignKey(x => x.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Dependency>()
            .HasOne(d => d.Predecessor)
            .WithMany(t => t.SuccessorDependencies)
            .HasForeignKey(d => d.PredecessorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Dependency>()
            .HasOne(d => d.Successor)
            .WithMany(t => t.PredecessorDependencies)
            .HasForeignKey(d => d.SuccessorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Author)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskEvaluation>()
        .HasIndex(e => e.TaskItemId)
        .IsUnique();

        modelBuilder.Entity<TaskEvaluation>()
            .HasOne(e => e.TaskItem)
            .WithMany()
            .HasForeignKey(e => e.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskEvaluation>()
            .HasOne(e => e.Teacher)
            .WithMany()
            .HasForeignKey(e => e.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskResult>()
    .HasOne(r => r.TaskItem)
    .WithMany()
    .HasForeignKey(r => r.TaskItemId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<TaskResult>()
    .HasOne(r => r.Author)
    .WithMany()
    .HasForeignKey(r => r.AuthorId)
    .OnDelete(DeleteBehavior.Restrict);


    }
}
