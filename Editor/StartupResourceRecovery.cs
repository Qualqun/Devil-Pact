using System;
using System.Threading.Tasks;

namespace DevilPact.Editor;

/// <summary>
/// On a fresh checkout, the editor can restore source scenes before newly compiled
/// prefabs have entered ResourceLibrary. Recover those sessions once startup finishes.
/// Never save the incomplete scene or replace its object/component GUIDs.
/// </summary>
[global::Editor.Mcp.McpToolset( "devil_pact", "Project resource recovery" )]
public static class StartupResourceRecovery
{
	private static bool startupChecked;
	private static bool recovering;

	// The first editor frame runs after startup asset compilation and scene restoration.
	[EditorEvent.Frame]
	public static void OnEditorFrame()
	{
		if ( startupChecked || Project.Current is null || Game.IsPlaying ||
			!SceneEditorSession.All.Any( session => session.Scene?.Source is SceneFile ) ) return;
		startupChecked = true;
		Recover();
	}

	[Menu( "Editor", "Devil Pact/Recover Missing Prefabs", "refresh" )]
	[ConCmd( "devil_pact_recover_prefabs" )]
	[global::Editor.Mcp.McpTool( "devil_pact_recover_assets" )]
	public static async void Recover()
	{
		if ( recovering || Game.IsPlaying ) return;
		recovering = true;
		try { await RecoverAsync(); }
		catch ( Exception error ) { Log.Warning( $"[Devil Pact] Resource recovery failed: {error}" ); }
		finally { recovering = false; }
	}

	private static async Task RecoverAsync()
	{
		var projectRoot = Project.Current.RootDirectory.FullName.Replace( '\\', '/' ).TrimEnd( '/' ) + "/";
		var localAssets = AssetSystem.All.Where( asset =>
			asset.AbsolutePath?.Replace( '\\', '/' ).StartsWith( projectRoot, StringComparison.OrdinalIgnoreCase ) == true ).ToArray();
		var pending = localAssets.Where( asset => asset.CanRecompile &&
			(!asset.IsCompiled || !asset.IsCompiledAndUpToDate) && asset.AssetType.FileExtension != "vmap" ).ToArray();

		var sessions = SceneEditorSession.All.Where( session =>
			session.Scene?.Source is SceneFile &&
			session.Scene.GetAllObjects( false ).Any( go =>
				go.Name.StartsWith( "[Missing Prefab] ", StringComparison.Ordinal ) ) ).ToArray();
		if ( pending.Length > 0 )
			sessions = SceneEditorSession.All.Where( session => session.Scene?.Source is SceneFile ).ToArray();
		if ( sessions.Length == 0 ) return;
		Log.Info( $"[Devil Pact] Preparing {pending.Length} uncompiled or outdated assets before restoring scenes." );

		// IsCompiledAndUpToDate can be true even when the compiled file is absent.
		// A successful Compile() only queues native compilation. Await the actual output
		// before loading prefabs, or ModelRenderer can retain an error model on a fresh clone.
		foreach ( var asset in pending )
		{
			asset.Compile( true );
			await asset.CompileIfNeededAsync( 120 );
			if ( !asset.IsCompiled )
			{
				Log.Warning( $"[Devil Pact] Could not compile {asset.Path}; scene recovery skipped." );
				return;
			}
		}

		// Compile/load first, then deserialize the untouched source scene again.
		// Loading all local prefabs also covers nested prefabs and gameplay references.
		foreach ( var asset in localAssets.Where( asset => asset.AssetType.FileExtension == "prefab" ) )
		{
			if ( (!asset.IsCompiled || !asset.IsCompiledAndUpToDate) && !asset.Compile( false ) )
			{
				Log.Warning( $"[Devil Pact] Could not compile {asset.Path}; scene recovery skipped." );
				return;
			}
			if ( asset.LoadResource<PrefabFile>() is null )
			{
				Log.Warning( $"[Devil Pact] Could not load {asset.Path}; scene recovery skipped." );
				return;
			}
		}

		foreach ( var session in sessions )
		{
			if ( Game.IsPlaying || !SceneEditorSession.All.Contains( session ) ) continue;
			if ( session.HasUnsavedChanges )
			{
				Log.Warning( "[Devil Pact] Scene has unsaved changes; automatic recovery skipped." );
				continue;
			}

			var path = session.Scene.Source.ResourcePath;
			var source = AssetSystem.FindByPath( path )?.LoadResource<SceneFile>();
			if ( source is null ) continue;
			session.Destroy();
			EditorScene.OpenScene( source );
			Log.Info( $"[Devil Pact] Reloaded {path} after loading its prefabs." );
		}
	}
}
