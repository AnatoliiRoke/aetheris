namespace Aetheris.Research.Services;

/// <summary>
/// The error and reason codes of the research module, sent to the client as localisation keys.
/// </summary>
public static class ResearchErrorCodes
{
    /// <summary>The technology does not exist.</summary>
    public const string TechnologyUnknown = "research_technology_unknown";

    /// <summary>The level of the technology does not exist.</summary>
    public const string LevelUnknown = "research_level_unknown";

    /// <summary>The record of the queue does not exist.</summary>
    public const string QueueItemUnknown = "research_queue_item_unknown";

    /// <summary>The queue of the researches has no free place.</summary>
    public const string QueueFull = "research_queue_full";

    /// <summary>The body does not have a science centre of the level the research needs.</summary>
    public const string ResearchCenterLevel = "research_center_level_unmet";

    /// <summary>The player has already learned the level of the technology.</summary>
    public const string AlreadyLearned = "research_already_learned";

    /// <summary>The body lacks the resources for the cost of the research.</summary>
    public const string ResourcesShort = "research_resources_short";

    /// <summary>The technology is already in the queue.</summary>
    public const string AlreadyQueued = "research_already_queued";
}

/// <summary>
/// What the preparation of the module did (task 00012).
/// </summary>
/// <param name="SeededTechnologies">The number of technologies the seed wrote.</param>
public sealed record ResearchBootstrapResult(int SeededTechnologies);
