namespace MeltFall
{
    /// <summary>
    /// Top-level play-state machine states owned by <see cref="LevelManager"/>.
    /// Mirrors design §12.2 / plan §4.
    /// </summary>
    public enum PlayState
    {
        /// <summary>Diorama at rest; player may survey, pan, select liquid, begin firing.</summary>
        Surveying,

        /// <summary>A firing touch is held; the stream is active and consuming fuel.</summary>
        Spraying,

        /// <summary>A liquid swap purge is in progress; firing is blocked and no fuel drains.</summary>
        PurgeDelay,

        /// <summary>Pieces / gems are falling and settling after a collapse.</summary>
        CollapsingResolving,

        /// <summary>Final settled state; firing and switching disabled, result pending.</summary>
        Resolved
    }

    /// <summary>How a material reacts when hit with a non-matching liquid (spec §4, §14).</summary>
    public enum WrongLiquidResponse
    {
        /// <summary>No integrity is removed (fuel is still burned by the gun).</summary>
        Ignore,

        /// <summary>A near-zero fraction of integrity is chipped off (data-driven, never hardcoded).</summary>
        Chip
    }

    /// <summary>Resolution status of a single goal gem.</summary>
    public enum GemStatus
    {
        /// <summary>Not yet resolved.</summary>
        Pending,

        /// <summary>Fell a real distance and settled safely — counts toward stars.</summary>
        Landed,

        /// <summary>Settled in a hazard (or unresolved at level end) — does not count.</summary>
        Lost
    }

    /// <summary>State of the liquid selector / gun with respect to firing readiness.</summary>
    public enum LiquidSelectorState
    {
        /// <summary>A liquid is active and ready; not firing.</summary>
        Idle,

        /// <summary>A newly selected liquid is loading (purge delay); firing blocked.</summary>
        Purging,

        /// <summary>The active liquid's stream is currently firing.</summary>
        ActiveFiring
    }

    /// <summary>Visibility state of the aim / impact indicator.</summary>
    public enum AimIndicatorState
    {
        /// <summary>No firing touch in progress; marker hidden.</summary>
        Hidden,

        /// <summary>A firing touch is in progress; marker shown at the aim point.</summary>
        Showing
    }

    /// <summary>Coarse fuel level bucket for HUD readout.</summary>
    public enum FuelLevel
    {
        /// <summary>Above the low-fuel threshold.</summary>
        Normal,

        /// <summary>At or below the low-fuel threshold but not empty.</summary>
        Low,

        /// <summary>Fully drained.</summary>
        Empty
    }
}
