using UnityEngine;

namespace Utils.Samples.Attributes
{
    /// <summary>
    /// [SOGroup] задаёт группу ассета в окне Tools > Scriptable Object Editor.
    /// Создать: ПКМ в Project → Create → Utils Samples → Example Config.
    /// </summary>
    [SOGroup("Samples")]
    [CreateAssetMenu(menuName = "Utils Samples/Example Config", fileName = "ExampleConfig")]
    public class ExampleSO : ScriptableObject
    {
        [SteppedRange(0f, 1f, 0.05f)] public float difficulty = 0.5f;
        [MinMaxRange(1f, 60f)] public Vector2 waveInterval = new Vector2(10f, 20f);
    }
}
