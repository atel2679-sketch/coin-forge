using UnityEngine;
using UnityEngine.UI;

public sealed class CoinForgeTutorialHand : MaskableGraphic
{
    // Outline of a hand with its index finger pointing up.
    static readonly Vector2[] Outline =
    {
        new Vector2(36, 7), new Vector2(77, 7), new Vector2(77, 20), new Vector2(86, 35),
        new Vector2(90, 55), new Vector2(87, 62), new Vector2(82, 64), new Vector2(76, 61),
        new Vector2(75, 67), new Vector2(69, 71), new Vector2(62, 67), new Vector2(61, 73),
        new Vector2(55, 77), new Vector2(47, 72), new Vector2(47, 94), new Vector2(44, 99),
        new Vector2(36, 99), new Vector2(32, 94), new Vector2(32, 52), new Vector2(24, 62),
        new Vector2(15, 61), new Vector2(11, 55), new Vector2(13, 49), new Vector2(29, 28),
        new Vector2(36, 18),
    };

    static readonly int[] Triangles =
    {
        24, 0, 1, 24, 1, 2, 24, 2, 3, 24, 3, 4, 24, 4, 5,
        24, 5, 6, 24, 6, 7, 24, 7, 8, 24, 8, 9, 24, 9, 10,
        24, 10, 11, 24, 11, 12, 24, 12, 13, 24, 13, 14, 24, 14, 15,
        24, 15, 16, 24, 16, 17, 24, 17, 18, 24, 18, 19, 24, 19, 20,
        24, 20, 21, 21, 22, 23, 21, 23, 24,
    };

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        foreach (Vector2 point in Outline)
        {
            Vector2 position = rect.min + Vector2.Scale(rect.size, point / 100);
            mesh.AddVert(position, color, Vector2.zero);
        }
        for (int i = 0; i < Triangles.Length; i += 3)
            mesh.AddTriangle(Triangles[i], Triangles[i + 1], Triangles[i + 2]);
    }
}
