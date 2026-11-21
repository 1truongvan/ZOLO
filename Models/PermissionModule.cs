namespace WebApplication1.Models;

public class PermissionModule
{
    public int Id { get; set; }
    public string ModuleKey { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<PermissionDetail> Details { get; set; } = new List<PermissionDetail>();
    public ICollection<RoleModulePermission> RolePermissions { get; set; } = new List<RoleModulePermission>();
}

public class PermissionDetail
{
    public int Id { get; set; }
    public int PermissionModuleId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public PermissionModule Module { get; set; } = null!;
    public ICollection<RolePermissionDetail> RolePermissions { get; set; } = new List<RolePermissionDetail>();
}

public class RoleModulePermission
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int PermissionModuleId { get; set; }
    public bool MenuVisible { get; set; }
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public PermissionModule Module { get; set; } = null!;
    public ICollection<RolePermissionDetail> Details { get; set; } = new List<RolePermissionDetail>();
}

public class RolePermissionDetail
{
    public int RoleModulePermissionId { get; set; }
    public int PermissionDetailId { get; set; }
    public bool IsGranted { get; set; }
    public RoleModulePermission RolePermission { get; set; } = null!;
    public PermissionDetail PermissionDetail { get; set; } = null!;
}
