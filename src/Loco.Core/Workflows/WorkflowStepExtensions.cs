namespace Loco.Core.Workflows
{
    /// <summary>
    /// Additional properties for WorkflowStep to support parallel execution engine.
    /// </summary>
    public partial class WorkflowStep
    {
        /// <summary>
        /// Command-line arguments for process execution.
        /// </summary>
        public string? Arguments { get; set; }

        /// <summary>
        /// Working directory for process execution.
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Source path for file operations (copy, move).
        /// </summary>
        public string? SourcePath
        {
            get => Source;
            set => Source = value;
        }

        /// <summary>
        /// Destination path for file operations (copy, move).
        /// </summary>
        public string? DestinationPath
        {
            get => Destination;
            set => Destination = value;
        }

        /// <summary>
        /// Whether to perform recursive file/directory operations.
        /// </summary>
        public bool? Recursive { get; set; }
    }
}
