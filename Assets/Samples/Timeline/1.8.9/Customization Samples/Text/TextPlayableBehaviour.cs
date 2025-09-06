#if TEXT_TRACK_REQUIRES_TEXTMESH_PRO

using System;
using UnityEngine;
using UnityEngine.Playables;
using Sirenix.OdinInspector;
namespace Timeline.Samples
{
    // Runtime representation of a TextClip.
    // The Serializable attribute is required to be animated by timeline, and used as a template.
    [Serializable]
    public class TextPlayableBehaviour : PlayableBehaviour
    {
        [Tooltip("The color of the text")]
        public Color color = Color.white;

        [Tooltip("The size of the font to use")]
        public int fontSize = 14;
        [TextArea(10, 20)]
        [Tooltip("The text to display")]
        public string text = "";
    }
}

#endif
