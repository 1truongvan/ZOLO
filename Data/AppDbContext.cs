
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using YourProject.Models;

namespace WebApplication1.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Bảng User
        public DbSet<User> Users { get; set; } = null!;
        //bảng RepairJob 
        // dùng để lưu trữ thông tin về các công việc sửa chữa, bao gồm các chi tiết như số công việc, trạng thái, chẩn đoán, ghi chú sửa chữa, thời gian tạo và hoàn thành, cũng như liên kết đến yêu cầu bảo hành và kỹ thuật viên thực hiện công việc.
        public DbSet<RepairJob> RepairJobs { get; set; } = null!;
        public DbSet<RepairJobUpdate> RepairJobUpdates { get; set; } = null!;
        public DbSet<RepairJobPart> RepairJobParts { get; set; } = null!;

        // Bảng Products
        public DbSet<Product> Products { get; set; } = null!;

        // Bảng StoredFile
        public DbSet<StoredFile> StoredFiles { get; set; } = null!;

        public DbSet<AdminCatalogEntry> AdminCatalogEntries { get; set; } = null!;
        public DbSet<InventoryBalance> InventoryBalances { get; set; } = null!;
        public DbSet<StockDocument> StockDocuments { get; set; } = null!;
        public DbSet<StockDocumentLine> StockDocumentLines { get; set; } = null!;
        public DbSet<StationPartBalance> StationPartBalances { get; set; } = null!;
        public DbSet<WarrantyRequest> WarrantyRequests { get; set; } = null!;
        public DbSet<WarrantyRequestAttachment> WarrantyRequestAttachments { get; set; } = null!;
        public DbSet<PermissionModule> PermissionModules { get; set; } = null!;
        public DbSet<PermissionDetail> PermissionDetails { get; set; } = null!;
        public DbSet<RoleModulePermission> RoleModulePermissions { get; set; } = null!;
        public DbSet<RolePermissionDetail> RolePermissionDetails { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);

            // Giữ nguyên tên bảng User
            modelBuilder.Entity<User>()
                .ToTable("User");

            // Giữ nguyên bảng Products
            modelBuilder.Entity<Product>()
                .ToTable("Products");
            modelBuilder.Entity<Product>().HasOne(product => product.Owner)
                .WithMany().HasForeignKey(product => product.OwnerUserId).OnDelete(DeleteBehavior.SetNull);

            // Đặt tên bảng StoredFile
            modelBuilder.Entity<StoredFile>()
                .ToTable("StoredFile");

            modelBuilder.Entity<AdminCatalogEntry>().ToTable("AdminCatalogEntries");
            modelBuilder.Entity<InventoryBalance>().ToTable("InventoryBalances", table =>
            {
                table.HasCheckConstraint("CK_InventoryBalances_OneItem", "([ProductId] IS NOT NULL AND [ComponentId] IS NULL) OR ([ProductId] IS NULL AND [ComponentId] IS NOT NULL)");
                table.HasCheckConstraint("CK_InventoryBalances_NonNegative", "[Quantity] >= 0");
            });
            modelBuilder.Entity<StockDocument>().ToTable("StockDocuments");
            modelBuilder.Entity<StockDocumentLine>().ToTable("StockDocumentLines");
            modelBuilder.Entity<StationPartBalance>().ToTable("StationPartBalances", table =>
                table.HasCheckConstraint("CK_StationPartBalances_ValidQuantities", "[AllocatedQuantity] >= 0 AND [UsedQuantity] >= 0 AND [UsedQuantity] <= [AllocatedQuantity]"));
            modelBuilder.Entity<WarrantyRequest>().ToTable("WarrantyRequests");
            modelBuilder.Entity<WarrantyRequest>().HasOne(request => request.FaultGroup).WithMany()
                .HasForeignKey(request => request.FaultGroupId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<WarrantyRequestAttachment>().ToTable("WarrantyRequestAttachments");
            modelBuilder.Entity<WarrantyRequestAttachment>().HasOne(attachment => attachment.WarrantyRequest)
                .WithMany(request => request.Attachments).HasForeignKey(attachment => attachment.WarrantyRequestId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<WarrantyRequestAttachment>().HasOne(attachment => attachment.StoredFile)
                .WithMany().HasForeignKey(attachment => attachment.StoredFileId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<WarrantyRequestAttachment>().HasIndex(attachment => new { attachment.WarrantyRequestId, attachment.StoredFileId }).IsUnique();
            modelBuilder.Entity<PermissionModule>().ToTable("PermissionModules");
            modelBuilder.Entity<PermissionDetail>().ToTable("PermissionDetails");
            modelBuilder.Entity<RoleModulePermission>().ToTable("RoleModulePermissions");
            modelBuilder.Entity<RolePermissionDetail>().ToTable("RolePermissionDetails");

            modelBuilder.Entity<PermissionModule>().HasIndex(module => module.ModuleKey).IsUnique();
            modelBuilder.Entity<PermissionModule>().Property(module => module.ModuleKey).HasMaxLength(80).IsRequired();
            modelBuilder.Entity<PermissionModule>().Property(module => module.GroupKey).HasMaxLength(80).IsRequired();
            modelBuilder.Entity<PermissionModule>().Property(module => module.Name).HasMaxLength(160).IsRequired();
            modelBuilder.Entity<PermissionDetail>().HasIndex(detail => new { detail.PermissionModuleId, detail.PermissionKey }).IsUnique();
            modelBuilder.Entity<PermissionDetail>().Property(detail => detail.PermissionKey).HasMaxLength(80).IsRequired();
            modelBuilder.Entity<PermissionDetail>().Property(detail => detail.Name).HasMaxLength(160).IsRequired();
            modelBuilder.Entity<PermissionDetail>().HasOne(detail => detail.Module).WithMany(module => module.Details)
                .HasForeignKey(detail => detail.PermissionModuleId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RoleModulePermission>().HasIndex(permission => new { permission.RoleName, permission.PermissionModuleId }).IsUnique();
            modelBuilder.Entity<RoleModulePermission>().Property(permission => permission.RoleName).HasMaxLength(40).IsRequired();
            modelBuilder.Entity<RoleModulePermission>().HasOne(permission => permission.Module).WithMany(module => module.RolePermissions)
                .HasForeignKey(permission => permission.PermissionModuleId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RoleModulePermission>().HasOne<User>().WithMany().HasForeignKey(permission => permission.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<RolePermissionDetail>().HasKey(detail => new { detail.RoleModulePermissionId, detail.PermissionDetailId });
            modelBuilder.Entity<RolePermissionDetail>().HasOne(detail => detail.RolePermission).WithMany(permission => permission.Details)
                .HasForeignKey(detail => detail.RoleModulePermissionId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RolePermissionDetail>().HasOne(detail => detail.PermissionDetail).WithMany(permission => permission.RolePermissions)
                .HasForeignKey(detail => detail.PermissionDetailId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AdminCatalogEntry>()
                .HasIndex(entry => new { entry.ResourceKey, entry.Code }).IsUnique();
            modelBuilder.Entity<AdminCatalogEntry>().Property(entry => entry.ResourceKey).HasMaxLength(40).IsRequired();
            modelBuilder.Entity<AdminCatalogEntry>().Property(entry => entry.Code).HasMaxLength(60).IsRequired();
            modelBuilder.Entity<AdminCatalogEntry>().Property(entry => entry.Name).HasMaxLength(150).IsRequired();
            modelBuilder.Entity<AdminCatalogEntry>().Property(entry => entry.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<AdminCatalogEntry>().Property(entry => entry.RangeFrom).HasPrecision(18, 2);
            modelBuilder.Entity<AdminCatalogEntry>().Property(entry => entry.RangeTo).HasPrecision(18, 2);
            modelBuilder.Entity<AdminCatalogEntry>().HasOne(entry => entry.ResponsibleUser)
                .WithMany().HasForeignKey(entry => entry.ResponsibleUserId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<InventoryBalance>().Property(balance => balance.RowVersion).IsRowVersion();
            modelBuilder.Entity<InventoryBalance>()
                .HasIndex(balance => new { balance.WarehouseId, balance.ProductId }).IsUnique()
                .HasFilter("[ProductId] IS NOT NULL");
            modelBuilder.Entity<InventoryBalance>()
                .HasIndex(balance => new { balance.WarehouseId, balance.ComponentId }).IsUnique()
                .HasFilter("[ComponentId] IS NOT NULL");
            modelBuilder.Entity<InventoryBalance>().HasOne(balance => balance.Warehouse)
                .WithMany().HasForeignKey(balance => balance.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventoryBalance>().HasOne(balance => balance.Product)
                .WithMany().HasForeignKey(balance => balance.ProductId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventoryBalance>().HasOne(balance => balance.Component)
                .WithMany().HasForeignKey(balance => balance.ComponentId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockDocument>().HasIndex(document => document.Code).IsUnique();
            modelBuilder.Entity<StockDocument>().HasOne(document => document.Warehouse)
                .WithMany().HasForeignKey(document => document.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockDocument>().HasOne(document => document.Station)
                .WithMany().HasForeignKey(document => document.StationId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockDocument>().HasOne(document => document.Dealer)
                .WithMany().HasForeignKey(document => document.DealerId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockDocument>().HasOne(document => document.CreatedBy)
                .WithMany().HasForeignKey(document => document.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockDocumentLine>().HasOne(line => line.Document)
                .WithMany(document => document.Lines).HasForeignKey(line => line.StockDocumentId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StockDocumentLine>().HasOne(line => line.Product)
                .WithMany().HasForeignKey(line => line.ProductId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockDocumentLine>().HasOne(line => line.Component)
                .WithMany().HasForeignKey(line => line.ComponentId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockDocumentLine>().Property(line => line.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<StationPartBalance>()
                .HasIndex(balance => new { balance.StationId, balance.ComponentId }).IsUnique();
            modelBuilder.Entity<StationPartBalance>().HasOne(balance => balance.Station)
                .WithMany().HasForeignKey(balance => balance.StationId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StationPartBalance>().HasOne(balance => balance.Component)
                .WithMany().HasForeignKey(balance => balance.ComponentId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<WarrantyRequest>().HasIndex(request => request.RequestCode).IsUnique();
            modelBuilder.Entity<WarrantyRequest>().HasOne(request => request.Customer)
                .WithMany().HasForeignKey(request => request.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<WarrantyRequest>().HasOne(request => request.AssignedTechnician)
                .WithMany().HasForeignKey(request => request.AssignedTechnicianId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<WarrantyRequest>().HasOne(request => request.Product)
                .WithMany().HasForeignKey(request => request.ProductId).OnDelete(DeleteBehavior.SetNull);

             modelBuilder.Entity<RepairJob>()
                .HasOne(r => r.WarrantyRequest)
                .WithMany()
                .HasForeignKey(r => r.WarrantyRequestId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RepairJobUpdate>().ToTable("RepairJobUpdates");
            modelBuilder.Entity<RepairJobUpdate>()
                .HasOne(update => update.RepairJob)
                .WithMany()
                .HasForeignKey(update => update.RepairJobId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RepairJobUpdate>()
                .HasOne(update => update.UpdatedByUser)
                .WithMany()
                .HasForeignKey(update => update.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<RepairJobPart>().ToTable("RepairJobParts");
            modelBuilder.Entity<RepairJobPart>().HasIndex(part => new { part.RepairJobId, part.ComponentId }).IsUnique();
            modelBuilder.Entity<RepairJobPart>().Property(part => part.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<RepairJobPart>()
                .HasOne(part => part.RepairJob)
                .WithMany()
                .HasForeignKey(part => part.RepairJobId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RepairJobPart>()
                .HasOne(part => part.Component)
                .WithMany()
                .HasForeignKey(part => part.ComponentId)
                .OnDelete(DeleteBehavior.Restrict);
            // Username không được trùng
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Email không được trùng
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}


