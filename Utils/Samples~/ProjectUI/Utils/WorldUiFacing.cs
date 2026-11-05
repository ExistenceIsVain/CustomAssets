namespace ProjectUI
{
    /// <summary>How a piece of UI standing in the world is turned to the camera.</summary>
    public enum WorldUiFacing
    {
        /// <summary>Parallel to the screen: it takes the camera's own rotation, so it never leans or shears.</summary>
        Screen,

        /// <summary>Its front looks at the camera's position.</summary>
        Camera,

        /// <summary>Its back looks at the camera's position (a canvas, whose front is its far side).</summary>
        AwayFromCamera,
    }
}
