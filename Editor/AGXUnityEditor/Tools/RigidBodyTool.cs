﻿using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AGXUnity;
using AGXUnity.Collide;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( RigidBody ) )]
  public class RigidBodyTool : CustomTargetTool
  {
    private List<Constraint> m_constraints = new List<Constraint>();

    public RigidBody RigidBody
    {
      get
      {
        return Targets[ 0 ] as RigidBody;
      }
    }

    public bool FindTransformGivenPointTool
    {
      get { return GetChild<FindPointTool>() != null; }
      set
      {
        if ( value && !FindTransformGivenPointTool ) {
          RemoveAllChildren();

          var pointTool = new FindPointTool();
          pointTool.OnPointFound = data =>
          {
            Undo.RecordObject( RigidBody.transform, "Rigid body transform" );

            RigidBody.transform.position = data.RaycastResult.Point;
            RigidBody.transform.rotation = data.Rotation;

            EditorUtility.SetDirty( RigidBody );
          };

          AddChild( pointTool );
        }
        else if ( !value )
          RemoveChild( GetChild<FindPointTool>() );
      }
    }

    public bool FindTransformGivenEdgeTool
    {
      get { return GetChild<EdgeDetectionTool>() != null; }
      set
      {
        if ( value && !FindTransformGivenEdgeTool ) {
          RemoveAllChildren();

          var edgeTool = new EdgeDetectionTool();
          edgeTool.OnEdgeFound = data =>
          {
            Undo.RecordObject( RigidBody.transform, "Rigid body transform" );

            RigidBody.transform.position = data.Position;
            RigidBody.transform.rotation = data.Rotation;

            EditorUtility.SetDirty( RigidBody );
          };

          AddChild( edgeTool );
        }
        else if ( !value )
          RemoveChild( GetChild<EdgeDetectionTool>() );
      }
    }

    public bool ShapeCreateTool
    {
      get { return GetChild<ShapeCreateTool>() != null; }
      set
      {
        if ( value && !ShapeCreateTool ) {
          RemoveAllChildren();

          var shapeCreateTool = new ShapeCreateTool( RigidBody.gameObject );
          AddChild( shapeCreateTool );
        }
        else if ( !value )
          RemoveChild( GetChild<ShapeCreateTool>() );
      }
    }

    public bool ConstraintCreateTool
    {
      get { return GetChild<ConstraintCreateTool>() != null; }
      set
      {
        if ( value && !ConstraintCreateTool ) {
          RemoveAllChildren();

          var constraintCreateTool = new ConstraintCreateTool( RigidBody.gameObject,
                                                               false,
                                                               newConstraint => m_constraints.Add( newConstraint ) );
          AddChild( constraintCreateTool );
        }
        else if ( !value )
          RemoveChild( GetChild<ConstraintCreateTool>() );
      }
    }

    public bool DisableCollisionsTool
    {
      get { return GetChild<DisableCollisionsTool>() != null; }
      set
      {
        if ( value && !DisableCollisionsTool ) {
          RemoveAllChildren();

          var disableCollisionsTool = new DisableCollisionsTool( RigidBody.gameObject );
          AddChild( disableCollisionsTool );
        }
        else if ( !value )
          RemoveChild( GetChild<DisableCollisionsTool>() );
      }
    }

    public bool RigidBodyVisualCreateTool
    {
      get { return GetChild<RigidBodyVisualCreateTool>() != null; }
      set
      {
        if ( value && !RigidBodyVisualCreateTool ) {
          RemoveAllChildren();

          var createRigidBodyVisualTool = new RigidBodyVisualCreateTool( RigidBody );
          AddChild( createRigidBodyVisualTool );
        }
        else if ( !value )
          RemoveChild( GetChild<RigidBodyVisualCreateTool>() );
      }
    }

    public bool ToolsActive = true;

    public RigidBodyTool( Object[] targets )
      : base( targets )
    {
#if UNITY_2019_1_OR_NEWER
      var allConstraints = StageUtility.GetCurrentStageHandle().Contains( RigidBody.gameObject ) ?
                             StageUtility.GetCurrentStageHandle().FindComponentsOfType<Constraint>() :
                             Object.FindObjectsOfType<Constraint>();
#else
      var allConstraints = Object.FindObjectsOfType<Constraint>();
#endif
      foreach ( var constraint in allConstraints ) {
        foreach ( var rb in GetTargets<RigidBody>() )
          if ( constraint.AttachmentPair.Contains( rb ) )
            m_constraints.Add( constraint );
      }
    }

    public override void OnAdd()
    {
      foreach ( var rb in GetTargets<RigidBody>() )
        rb.MassProperties.OnForcedMassInertiaUpdate();
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      int rbIndex = 0;
      foreach ( var rb in GetTargets<RigidBody>() ) {
        var cmPosition = rb.transform.TransformPoint( rb.MassProperties.CenterOfMassOffset.Value );
        var cmTransformToolVisible = !rb.MassProperties.CenterOfMassOffset.UseDefault;
        if ( cmTransformToolVisible ) {
          var newPosition = PositionTool( cmPosition, rb.transform.rotation, 0.6f, 1.0f );
          if ( Vector3.SqrMagnitude( cmPosition - newPosition ) > 1.0E-6 ) {
            cmPosition = newPosition;
            rb.MassProperties.CenterOfMassOffset.UserValue = rb.transform.InverseTransformPoint( newPosition );
          }
        }

        var rbId       = "rb_vis_" + (rbIndex++).ToString();
        var vp         = GetOrCreateVisualPrimitive<Utils.VisualPrimitiveSphere>( rbId, "GUI/Text Shader" );
        vp.Color       = new Color( 0, 0, 1, 0.25f );
        vp.Visible     = true;
        vp.Pickable    = false;
        vp.SetTransform( cmPosition,
                         rb.transform.rotation,
                         0.05f,
                         true,
                         0.0f,
                         0.25f );
        var shapes = rb.GetComponentsInChildren<Shape>();
        if ( shapes.Length < 2 )
          continue;
        int shapeIndex = 0;
        foreach ( var shape in shapes ) {
          var shapeLine = GetOrCreateVisualPrimitive<Utils.VisualPrimitiveCylinder>( rbId + "_shape_" + (shapeIndex++).ToString(), "GUI/Text Shader" );
          shapeLine.Color = new Color( 0, 1, 0, 0.05f );
          shapeLine.Visible = true;
          shapeLine.Pickable = false;
          shapeLine.SetTransform( cmPosition, shape.transform.position, 0.015f );
        }
      }
    }

    public override void OnPreTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;

      bool toggleShapeCreate             = false;
      bool toggleConstraintCreate        = false;
      bool toggleDisableCollisions       = false;
      bool toggleRigidBodyVisualCreate   = false;

      if ( !IsMultiSelect && ToolsActive ) {
        InspectorGUI.ToolButtons( InspectorGUI.ToolButtonData.Create( ToolIcon.CreateConstraint,
                                                                      ConstraintCreateTool,
                                                                      "Create new constraint to this rigid body.",
                                                                      () => toggleConstraintCreate = true ),
                                  InspectorGUI.ToolButtonData.Create( ToolIcon.DisableCollisions,
                                                                      DisableCollisionsTool,
                                                                      "Disable collisions against other objects.",
                                                                      () => toggleDisableCollisions = true ),
                                  InspectorGUI.ToolButtonData.Create( ToolIcon.CreateShapeGivenVisual,
                                                                      ShapeCreateTool,
                                                                      "Create shape from child visual object.",
                                                                      () => toggleShapeCreate = true ),
                                  InspectorGUI.ToolButtonData.Create( ToolIcon.CreateVisual,
                                                                      RigidBodyVisualCreateTool,
                                                                      "Create visual representation of each physical shape in this body.",
                                                                      () => toggleRigidBodyVisualCreate = true,
                                                                      Tools.RigidBodyVisualCreateTool.ValidForNewShapeVisuals( RigidBody ) ) );
      }

      if ( ConstraintCreateTool ) {
        GetChild<ConstraintCreateTool>().OnInspectorGUI();
      }
      if ( DisableCollisionsTool ) {
        GetChild<DisableCollisionsTool>().OnInspectorGUI();
      }
      if ( ShapeCreateTool ) {
        GetChild<ShapeCreateTool>().OnInspectorGUI();
      }
      if ( RigidBodyVisualCreateTool ) {
        GetChild<RigidBodyVisualCreateTool>().OnInspectorGUI();
      }

      EditorGUILayout.LabelField( GUI.MakeLabel( "Mass properties", true ), skin.Label );
      using ( InspectorGUI.IndentScope.Single )
        InspectorEditor.DrawMembersGUI( GetTargets<RigidBody>().Select( rb => rb.MassProperties ).ToArray() );

      if ( toggleConstraintCreate )
        ConstraintCreateTool = !ConstraintCreateTool;
      if ( toggleDisableCollisions )
        DisableCollisionsTool = !DisableCollisionsTool;
      if ( toggleShapeCreate )
        ShapeCreateTool = !ShapeCreateTool;
      if ( toggleRigidBodyVisualCreate )
        RigidBodyVisualCreateTool = !RigidBodyVisualCreateTool;
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( IsMultiSelect )
        return;

      InspectorGUI.ToolArrayGUI( this, RigidBody.Shapes, "Shapes" );

      InspectorGUI.ToolArrayGUI( this, m_constraints.ToArray(), "Constraints" );
    }
  }
}
