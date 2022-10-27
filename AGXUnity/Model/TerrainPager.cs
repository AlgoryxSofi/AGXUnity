using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  [Serializable]
  public class PagingBody<T>
  {
    public T Body;
    public float requiredRadius;
    public float preloadRadius;

    public PagingBody( T body, float requiredRadius, float preloadRadius )
    {
      Body = body;
      this.requiredRadius = requiredRadius;
      this.preloadRadius = preloadRadius;
    }
  }

  [AddComponentMenu( "AGXUnity/Model/Terrain Pager" )]
  [RequireComponent( typeof( Terrain ) )]
  [DisallowMultipleComponent]
  public class TerrainPager : ScriptComponent, ITerrain
  {
    /// <summary>
    /// Native TerrainPager instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.TerrainPager Native { get; private set; } = null;

    [SerializeField]
    private List<PagingBody<DeformableTerrainShovel>> m_shovels = new();

    /// <summary>
    /// Shovels associated to this terrain.
    /// </summary>
    [HideInInspector]
    public DeformableTerrainShovel[] Shovels { get { return m_shovels.Select( shovel => shovel.Body ).ToArray(); } }

    /// <summary>
    /// Shovels along with their respective load radii that are associated with this terrainPager
    /// </summary>
    /// <remarks>
    /// Do not attempt to modify the load-radii by modifying this list, instead use <see cref="SetTileLoadRadius(DeformableTerrainShovel,float,float)"/>
    /// </remarks>
    [HideInInspector]
    public PagingBody<DeformableTerrainShovel>[] PagingShovels { get { return m_shovels.ToArray(); } }

    [SerializeField]
    private List<PagingBody<RigidBody>> m_rigidbodies = new();

    /// <summary>
    /// Rigidbodies associated to this terrain.
    /// </summary>
    [HideInInspector]
    public RigidBody[] RigidBodies { get { return m_rigidbodies.Select( rb => rb.Body ).ToArray(); } }

    /// <summary>
    /// Rigidbodies along with their respective load radii that are associated with this terrainPager
    /// </summary>
    /// <remarks>
    /// Do not attempt to modify the load-radii by modifying this list, instead use <see cref="SetTileLoadRadius(RigidBody,float,float)"/>
    /// </remarks>
    [HideInInspector]
    public PagingBody<RigidBody>[] PagingRigidBodies { get { return m_rigidbodies.ToArray(); } }

    /// <summary>
    /// Unity Terrain component.
    /// </summary>
    public Terrain Terrain
    {
      get
      {
        return m_terrain == null ?
                 m_terrain = GetComponent<Terrain>() :
                 m_terrain;
      }
    }

    /// <summary>
    /// Unity Terrain data.
    /// </summary>
    [HideInInspector]
    public TerrainData TerrainData { get { return Terrain?.terrainData; } }

    /// <summary>
    /// Unity Terrain heightmap resolution.
    /// </summary>
    [HideInInspector]
    public int TerrainDataResolution { get { return TerrainUtils.TerrainDataResolution( TerrainData ); } }

    /// <summary>
    /// Size in units which each heightmap texel represent
    /// </summary>
    public float ElementSize
    {
      get
      {
        return TerrainData.size.x / ( TerrainDataResolution - 1 );
      }
    }

    [SerializeField]
    private ShapeMaterial m_material = null;

    /// <summary>
    /// Shape material associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material
    {
      get { return m_material; }
      set
      {
        m_material = value;
        if ( Native != null ) {
          if ( m_material != null && m_material.Native == null )
            m_material.GetInitialized<ShapeMaterial>();
          if ( m_material != null )
            Native.getTemplateTerrain().setMaterial( m_material.Native );

          // TODO: When m_material is null here it means "use default" but
          //       it's currently not possible to understand which parameters
          //       that has been set in e.g., Terrain::loadLibraryMaterial.
        }
      }
    }

    [SerializeField]
    private DeformableTerrainMaterial m_terrainMaterial = null;

    /// <summary>
    /// Terrain material associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    public DeformableTerrainMaterial TerrainMaterial
    {
      get { return m_terrainMaterial; }
      set
      {
        m_terrainMaterial = value;

        if ( Native != null ) {
          if ( m_terrainMaterial != null )
            Native.getTemplateTerrain().setTerrainMaterial( m_terrainMaterial.GetInitialized<DeformableTerrainMaterial>().Native );
          else
            Native.getTemplateTerrain().setTerrainMaterial( DeformableTerrainMaterial.CreateNative( "dirt_1" ) );
          Native.applyChangesToTemplateTerrain();
        }
      }
    }

    [SerializeField]
    private DeformableTerrainProperties m_properties = null;

    /// <summary>
    /// Terrain properties associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    public DeformableTerrainProperties Properties
    {
      get { return m_properties; }
      set
      {
        if ( Native != null && m_properties != null )
          m_properties.Unregister( this );

        m_properties = value;

        if ( Native != null && m_properties != null )
          m_properties.Register( this );

        Native.applyChangesToTemplateTerrain();
      }
    }

    [SerializeField]
    private float m_maximumDepth = 20.0f;

    /// <summary>
    /// Maximum depth, it's not possible to dig deeper than this value.
    /// This game object will be moved down MaximumDepth and MaximumDepth
    /// will be added to the heights.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector( true )]
    public float MaximumDepth
    {
      get { return m_maximumDepth; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "DeformableTerrain MaximumDepth: Value is used during initialization" +
                            " and cannot be changed when the terrain has been initialized.", this );
          return;
        }
        m_maximumDepth = value;
      }
    }

    /// <summary>
    /// The size of the underlying AGX Terrain tiles
    /// </summary>
    [ClampAboveZeroInInspector]
    [field: SerializeField]
    public int TileSize { get; set; } = 97;

    /// <summary>
    /// The overlap of adjacent AGX Terrain tiles
    /// </summary>
    [ClampAboveZeroInInspector]
    [field: SerializeField]
    public int TileOverlap { get; set; } = 16;

    /// <summary>
    /// Associates the given shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to add.</param>
    /// <param name="requiredRadius">The radius around the shovel instance where the terrain tiles are required to be loaded.</param>
    /// <param name="preloadRadius">The radius around the shovel instance for which to preload terrain tiles</param>
    /// <returns>True if added, false if null or already added</returns>
    public bool Add( DeformableTerrainShovel shovel, float requiredRadius = 10, float preloadRadius = 10 )
    {
      if ( shovel == null || m_shovels.Find( pagingShovel => pagingShovel.Body == shovel ) != null )
        return false;

      PagingBody<DeformableTerrainShovel> pb = new(shovel, requiredRadius, preloadRadius);

      m_shovels.Add( pb );

      // Initialize shovel if we're initialized.
      if ( Native != null )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>().Native, requiredRadius, preloadRadius );

      return true;
    }

    /// <summary>
    /// Disassociate shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this terrain.</returns>
    public bool Remove( DeformableTerrainShovel shovel )
    {
      if ( shovel == null || m_shovels.Find( pagingShovel => pagingShovel.Body == shovel ) == null )
        return false;

      if ( Native != null )
        Native.remove( shovel.Native );

      m_shovels.RemoveAt( m_shovels.FindIndex( pagingShovel => pagingShovel.Body == shovel ) );
      return true;
    }

    /// <summary>
    /// Associates the given rigidbody instance to this terrain.
    /// </summary>
    /// <param name="rigidbody">Rigidbody instance to add.</param>
    /// <param name="requiredRadius">The radius around the rigidbody instance where the terrain tiles are required to be loaded.</param>
    /// <param name="preloadRadius">The radius around the rigidbody instance for which to preload terrain tiles</param>
    /// <returns>True if added, false if null or already added</returns>
    public bool Add( RigidBody rigidbody, float requiredRadius = 10, float preloadRadius = 10 )
    {
      if ( rigidbody == null || m_rigidbodies.Find( pagingRigidBody => pagingRigidBody.Body == rigidbody ) != null )
        return false;

      PagingBody<RigidBody> pb = new(rigidbody, requiredRadius, preloadRadius);

      m_rigidbodies.Add( pb );

      // Initialize shovel if we're initialized.
      if ( Native != null )
        Native.add( rigidbody.GetInitialized<RigidBody>().Native, requiredRadius, preloadRadius );

      return true;
    }

    /// <summary>
    /// Disassociate rigidbody instance to this terrain.
    /// </summary>
    /// <param name="rigidbody">Rigidbody instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this terrain.</returns>
    public bool Remove( RigidBody rigidbody )
    {
      if ( rigidbody == null || m_rigidbodies.Find( pagingRigidBody => pagingRigidBody.Body == rigidbody ) == null )
        return false;

      if ( Native != null )
        Native.remove( rigidbody.Native );

      m_rigidbodies.RemoveAt( m_rigidbodies.FindIndex( pagingRigidBody => pagingRigidBody.Body == rigidbody ) );
      return true;
    }

    /// <summary>
    /// Gets the tile load radii associated with the provided shovel
    /// </summary>
    /// <param name="shovel">The shovel to get the tile load radii for</param>
    /// <returns>The tile load radii associated with the shovel or (-1,-1) if shovel is not associated with pager</returns>
    public Vector2 GetTileLoadRadius( DeformableTerrainShovel shovel )
    {
      var pagingShovel = m_shovels.Find(pb => pb.Body == shovel);
      if ( pagingShovel != null )
        return new Vector2( pagingShovel.requiredRadius, pagingShovel.preloadRadius );

      if ( Native == null ) return new Vector2( -1, -1 );

      var radii = Native.getTileLoadRadius( shovel.RigidBody.Native );
      return new Vector2( (float)radii.first, (float)radii.second );
    }

    /// <summary>
    /// Sets the tile load radii associated with the provided shovel
    /// </summary>
    /// <param name="shovel">The shovel to set the tile load radii for</param>
    /// <param name="requiredRadius">The radius within which all terrain tiles must be loaded</param>
    /// <param name="preloadRadius">The radius within which to start preloading terrain tiles</param>
    public void SetTileLoadRadius( DeformableTerrainShovel shovel, float requiredRadius, float preloadRadius )
    {
      var pagingShovel = m_shovels.Find(pb => pb.Body == shovel);
      if ( pagingShovel != null ) {
        pagingShovel.requiredRadius = requiredRadius;
        pagingShovel.preloadRadius = preloadRadius;
      }

      if ( Native == null ) return;
      Native.setTileLoadRadiuses( shovel.RigidBody.Native, requiredRadius, preloadRadius );
    }

    /// <summary>
    /// Gets the tile load radii associated with the provided rigidbody
    /// </summary>
    /// <param name="rigidbody">The rigidbody to get the tile load radii for</param>
    /// <returns>The tile load radii associated with the rigidbody or (-1,-1) if rigidbody is not associated with pager</returns>
    public Vector2 GetTileLoadRadius( RigidBody rigidbody )
    {
      var pagingRigidBody = m_rigidbodies.Find(pb => pb.Body == rigidbody);
      if ( pagingRigidBody != null )
        return new Vector2( pagingRigidBody.requiredRadius, pagingRigidBody.preloadRadius );

      if ( Native == null ) return new Vector2( -1, -1 );

      var radii = Native.getTileLoadRadius( rigidbody.Native );
      return new Vector2( (float)radii.first, (float)radii.second );
    }

    /// <summary>
    /// Sets the tile load radii associated with the provided rigidbody
    /// </summary>
    /// <param name="rigidbody">The rigidbody to set the tile load radii for</param>
    /// <param name="requiredRadius">The radius within which all terrain tiles must be loaded</param>
    /// <param name="preloadRadius">The radius within which to start preloading terrain tiles</param>
    public void SetTileLoadRadius( RigidBody rigidbody, float requiredRadius, float preloadRadius )
    {
      var pagingRigidBody = m_rigidbodies.Find(pb => pb.Body == rigidbody);
      if ( pagingRigidBody != null ) {
        pagingRigidBody.requiredRadius = requiredRadius;
        pagingRigidBody.preloadRadius = preloadRadius;
      }

      if ( Native == null ) return;
      Native.setTileLoadRadiuses( rigidbody.Native, requiredRadius, preloadRadius );
    }

    /// <summary>
    /// Verifies so that all added bodies still exists. Bodies that
    /// has been deleted are removed.
    /// </summary>
    public void RemoveInvalidBodies()
    {
      m_shovels.RemoveAll( shovel => shovel == null );
      m_rigidbodies.RemoveAll( rb => rb == null );
    }

    /// <summary>
    /// Checks if the current TerrainPager parameters tile the underlying Unity Terrain
    /// The amount of tiles R can be calculated as (l - O - 1) / (S - O - 1) where l is heightmap size O is overlap and S is tile size
    /// Parameters are valid if O and S tile l, that is if R is an integer
    /// </summary>
    /// <returns>True if the parameters tile the Unity Terrain</returns>
    public bool ValidateParameters()
    {
      float r = (float)(TerrainDataResolution - TileOverlap - 1) / (TileSize - TileOverlap - 1);
      return Mathf.Approximately( r, Mathf.Round( r ) );
    }

    protected override bool Initialize()
    {
      // Only printing the errors if something is wrong.
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTerrain | LicenseInfo.Module.AGXGranular, this );

      RemoveInvalidBodies();

      m_terrainDataSource = new UnityTerrainAdapter( Terrain, MaximumDepth );

      InitializeNative();

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      // Native terrain may change the number of PPGS iterations to default (25).
      // Override if we have solver settings set to the simulation.
      if ( Simulation.Instance.SolverSettings != null )
        GetSimulation().getSolver().setNumPPGSRestingIterations( (ulong)Simulation.Instance.SolverSettings.PpgsRestingIterations );

      SetNativeEnable( isActiveAndEnabled );

      return true;
    }

    private void InitializeNative()
    {
      if ( TerrainData.size.x != TerrainData.size.z )
        Debug.LogError( "Unity Terrain is not square, this is not supported" );

      if ( !ValidateParameters() )
        Debug.LogWarning( "Tile settings used does not fill the Unity terrain" );

      Vector3 rootPos =  GetComponent<TerrainConnector>().GetOffsetPosition();

      Native = new agxTerrain.TerrainPager(
        (uint)TileSize,
        (uint)TileOverlap,
        ElementSize,
        MaximumDepth,
        rootPos.ToHandedVec3(),
        agx.Quat.rotate( Mathf.PI, agx.Vec3.Z_AXIS() )
        * agx.Quat.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() ),
        new agxTerrain.Terrain( 10, 10, 1, 0.0f ) );

      Native.setTerrainDataSource( m_terrainDataSource );

      // Add Rigidbodies and shovels to pager
      foreach ( var shovel in m_shovels )
        Native.add( shovel.Body.GetInitialized<DeformableTerrainShovel>().Native, shovel.requiredRadius, shovel.preloadRadius );
      foreach ( var rb in m_rigidbodies )
        Native.add( rb.Body.GetInitialized<RigidBody>().Native, rb.requiredRadius, rb.preloadRadius );

      GetSimulation().add( Native );
    }

    private void SetNativeEnable( bool enable )
    {
      if ( Native == null )
        return;

      if ( Native.isEnabled() == enable )
        return;

      Native.setEnable( enable );
      foreach ( var tile in Native.getActiveTileAttachments() ) {
        var terr = tile.m_terrainTile;
        terr.setEnable( enable );
        terr.getGeometry().setEnable( enable );
      }
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }
      Native = null;

      base.OnDestroy();
    }

    protected override void OnEnable()
    {
      SetNativeEnable( true );
    }

    protected override void OnDisable()
    {
      SetNativeEnable( false );
    }

    private void OnPostStepForward()
    {
      m_terrainDataSource.Update();
      UpdateHeights();
    }

    private void UpdateHeights()
    {
      var tiles = Native.getActiveTileAttachments();
      foreach ( var tile in tiles ) {
        DebugDrawTile( tile );
        UpdateTerrain( tile );
      }
      TerrainData.SyncHeightmap();
    }

    private void UpdateTerrain( agxTerrain.TerrainPager.TileAttachments tile )
    {
      var terrain = tile.m_terrainTile;
      var modifications = terrain.getModifiedVertices();
      if ( modifications.Count == 0 )
        return;

      // We need to fetch the offset of the terrain tile since the TerrainPager
      // uses the height value of the data source when positioning the tiles.
      var scale = TerrainData.heightmapScale.y;
      var zOffset = tile.m_zOffset;
      var result = new float[,] { { 0.0f } };

      foreach ( var index in modifications ) {
        var gi = GetGlobalIndex( terrain, index );
        float h = (float)(terrain.getHeight( index ) + zOffset);

        result[ 0, 0 ] = h / scale;

        m_terrainDataSource.SetUnityHeightDelayed( result, gi );
      }
    }

    private Vector2Int GetGlobalIndex( agxTerrain.TerrainRef terrain, agx.Vec2i index )
    {
      var relTilePos = terrain.getPosition().ToHandedVector3() - transform.position;
      var elementsPerTile = TileSize - TileOverlap - 1;
      float tileOffset = elementsPerTile * ElementSize;
      Vector2Int tileIndex = new( Mathf.FloorToInt( relTilePos.x / tileOffset ), Mathf.FloorToInt( relTilePos.z / tileOffset ) );
      tileIndex *= elementsPerTile;
      tileIndex.x += (int)index.x;
      tileIndex.y += (int)index.y;
      return tileIndex;
    }


    // Remove this:
    private void DebugDrawTile( agxTerrain.TerrainPager.TileAttachments terr )
    {
      Vector3 basePos = terr.m_terrainTile.getPosition().ToHandedVector3();
      var size = terr.m_terrainTile.getSize() / 2;

      Vector3 v0 = basePos + new Vector3( (float)size.x,  0.1f - basePos.y, (float)size.y );
      Vector3 v1 = basePos + new Vector3( (float)size.x,  0.1f - basePos.y, (float)-size.y );
      Vector3 v2 = basePos + new Vector3( (float)-size.x, 0.1f - basePos.y, (float)-size.y );
      Vector3 v3 = basePos + new Vector3( (float)-size.x, 0.1f - basePos.y, (float)size.y );

      Debug.DrawLine( v0, v1 );
      Debug.DrawLine( v1, v2 );
      Debug.DrawLine( v2, v3 );
      Debug.DrawLine( v3, v0 );
    }

    public agx.GranularBodyPtrArray GetParticles()
    {
      return Native?.getSoilSimulationInterface()?.getSoilParticles();
    }

    public agxTerrain.TerrainProperties GetProperties()
    {
      return Native?.getTemplateTerrain().getProperties();
    }

    public void OnPropertiesUpdated()
    {
      if ( Native != null )
        Native.applyChangesToTemplateTerrain();
    }

    private Terrain m_terrain = null;
    private UnityTerrainAdapter m_terrainDataSource = null;
  }
}
