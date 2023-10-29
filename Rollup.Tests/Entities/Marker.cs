using RollupLibrary.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rollup.Tests.Entities;

[Table("dbo.ChangeTrackingMarker")]
internal class Marker : IMarker
{    
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public long Version { get; set; }
    public DateTime LastSyncUtc { get; set; }
}
