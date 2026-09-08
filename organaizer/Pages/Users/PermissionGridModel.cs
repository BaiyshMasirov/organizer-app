using organaizer.Infrastructure;

namespace organaizer.Pages.Users;

public sealed record PermissionGridModel(IReadOnlyList<PermissionSection> Sections, HashSet<string> Selected, string FieldName);
