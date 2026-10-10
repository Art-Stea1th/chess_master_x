using System.Numerics;
using Raylib_cs;

const int squares = 8;
const float square = 1f;
const float board = squares * square;
const float origin = -board / 2f + square / 2f;

Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
Raylib.InitWindow(960, 640, "Шахматы");
Raylib.SetTargetFPS(60);

var camera = new Camera3D
{
    Position = new Vector3(9f, 7f, 9f),
    Target = Vector3.Zero,
    Up = Vector3.UnitY,
    FovY = 42,
    Projection = CameraProjection.Perspective,
};

var background = new Color((byte)7, (byte)7, (byte)8, (byte)255);
var lightSquare = new Color((byte)214, (byte)186, (byte)140, (byte)255);
var darkSquare = new Color((byte)86, (byte)54, (byte)36, (byte)255);
var frame = new Color((byte)19, (byte)19, (byte)20, (byte)255);
var white = new Color((byte)232, (byte)226, (byte)214, (byte)255);
var black = new Color((byte)36, (byte)32, (byte)30, (byte)255);
var caption = new Color((byte)175, (byte)175, (byte)177, (byte)255);

while (!Raylib.WindowShouldClose())
{
    float angle = (float)Raylib.GetTime() * 0.35f;
    camera.Position = new Vector3(MathF.Cos(angle) * 9.2f, 6.4f, MathF.Sin(angle) * 9.2f);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(background);
    Raylib.BeginMode3D(camera);

    Raylib.DrawCube(new Vector3(0, -0.22f, 0), board + 0.7f, 0.36f, board + 0.7f, frame);

    for (int file = 0; file < squares; file++)
    {
        for (int rank = 0; rank < squares; rank++)
        {
            bool light = ((file + rank) & 1) == 1;
            var pos = new Vector3(origin + file * square, 0.08f, origin + rank * square);
            Raylib.DrawCube(pos, square, 0.16f, square, light ? lightSquare : darkSquare);
        }
    }

    DrawPawn(new Vector3(origin + 4 * square, 0.16f, origin + 3 * square), white);
    DrawPawn(new Vector3(origin + 4 * square, 0.16f, origin + 4 * square), black);

    Raylib.EndMode3D();
    Raylib.DrawText("raylib", 24, 18, 20, caption);
    Raylib.EndDrawing();
}

Raylib.CloseWindow();

static void DrawPawn(Vector3 cell, Color color)
{
    Raylib.DrawCylinder(cell, 0.28f, 0.34f, 0.52f, 20, color);
    Raylib.DrawSphere(new Vector3(cell.X, cell.Y + 0.68f, cell.Z), 0.2f, color);
}
