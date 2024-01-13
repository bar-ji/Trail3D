using Godot;
using System;

[Tool]
public partial class TrailPlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		var script = GD.Load<Script>("res://addons/Trail3D/Trail3D.cs");
		var texture = GD.Load<Texture2D>("res://addons/Trail3D/Icon.png");
		AddCustomType("Trail3D", "Node3D", script, texture);
	}

	public override void _ExitTree()
	{
		RemoveCustomType("Trail3D");
	}
}
