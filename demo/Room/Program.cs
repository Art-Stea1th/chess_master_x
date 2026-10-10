using System.Numerics;
using Raylib_cs;

const float room = 14f;
const float wall = 4.6f;
const float tableW = 4.8f;
const float tableD = 3.0f;
const float tableH = 0.82f;

Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
Raylib.InitWindow(1280, 800, "Шахматы");
Raylib.SetTargetFPS(60);

var screen = Raylib.LoadTexture("Assets/chesscom.png");
Raylib.SetTextureFilter(screen, TextureFilter.Bilinear);
var mesh = Raylib.GenMeshPlane(tableW - 0.22f, tableD - 0.22f, 1, 1);
var plate = Raylib.LoadModelFromMesh(mesh);
Raylib.SetMaterialTexture(ref plate, 0, MaterialMapIndex.Albedo, ref screen);

var camera = new Camera3D
{
    Position = new Vector3(5.4f, 2.8f, 5.4f),
    Target = new Vector3(0, tableH, 0),
    Up = Vector3.UnitY,
    FovY = 42,
    Projection = CameraProjection.Perspective,
};

var floor = new Color((byte)18, (byte)16, (byte)14, (byte)255);
var wallColor = new Color((byte)28, (byte)26, (byte)24, (byte)255);
var wood = new Color((byte)62, (byte)42, (byte)28, (byte)255);
var woodDark = new Color((byte)36, (byte)24, (byte)16, (byte)255);
var lamp = new Color((byte)232, (byte)198, (byte)142, (byte)255);
var caption = new Color((byte)175, (byte)175, (byte)177, (byte)255);

while (!Raylib.WindowShouldClose())
{
    float t = (float)Raylib.GetTime() * 0.22f;
    camera.Position = new Vector3(MathF.Cos(t) * 6.1f, 3.05f, MathF.Sin(t) * 6.1f);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color((byte)7, (byte)7, (byte)8, (byte)255));
    Raylib.BeginMode3D(camera);

    Raylib.DrawCube(new Vector3(0, -0.06f, 0), room, 0.12f, room, floor);
    Raylib.DrawCube(new Vector3(0, wall / 2f, -room / 2f), room, wall, 0.16f, wallColor);
    Raylib.DrawCube(new Vector3(0, wall / 2f, room / 2f), room, wall, 0.16f, wallColor);
    Raylib.DrawCube(new Vector3(-room / 2f, wall / 2f, 0), 0.16f, wall, room, wallColor);
    Raylib.DrawCube(new Vector3(room / 2f, wall / 2f, 0), 0.16f, wall, room, wallColor);
    Raylib.DrawCube(new Vector3(0, wall, 0), room, 0.12f, room, new Color((byte)14, (byte)13, (byte)12, (byte)255));

    DrawTable(wood, woodDark);
    Raylib.DrawModel(plate, new Vector3(0, tableH + 0.03f, 0), 1f, Color.White);

    Raylib.DrawSphere(new Vector3(0, wall - 0.7f, 0), 0.18f, lamp);
    Raylib.DrawCylinder(new Vector3(0, wall - 0.55f, 0), 0.04f, 0.04f, 0.55f, 8, woodDark);
    Raylib.DrawCylinderEx(new Vector3(0, tableH + 0.02f, 0), new Vector3(0, 0.02f, 0), 2.6f, 3.4f, 24, new Color((byte)40, (byte)28, (byte)16, (byte)90));

    Raylib.EndMode3D();
    Raylib.DrawText("chess.com", 24, 18, 20, caption);
    Raylib.EndDrawing();
}

Raylib.UnloadModel(plate);
Raylib.UnloadTexture(screen);
Raylib.CloseWindow();

static void DrawTable(Color wood, Color woodDark)
{
    Raylib.DrawCube(new Vector3(0, tableH - 0.06f, 0), tableW, 0.12f, tableD, wood);
    Raylib.DrawCube(new Vector3(0, tableH - 0.14f, 0), tableW + 0.12f, 0.06f, tableD + 0.12f, woodDark);
    foreach (var x in new[] { -tableW / 2f + 0.18f, tableW / 2f - 0.18f })
    {
        foreach (var z in new[] { -tableD / 2f + 0.18f, tableD / 2f - 0.18f })
        {
            Raylib.DrawCube(new Vector3(x, (tableH - 0.2f) / 2f, z), 0.12f, tableH - 0.2f, 0.12f, woodDark);
        }
    }
}
