namespace Core.Models
{
    /// <summary>
    /// Interface for objects that have a unique identifier
    /// </summary>
    public interface IIdentifiable
    {
        /// <summary>
        /// Gets or sets the unique identifier
        /// </summary>
        string Id { get; set; }
    }
}
