﻿using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

using Object = UnityEngine.Object;

namespace AGXUnityEditor
{
  public class InspectorEditor : Editor
  {
    public static InspectorGUISkin Skin
    {
      get
      {
        return InspectorGUISkin.Instance;
      }
    }

    /// <summary>
    /// True to force repaint of all InspectorEditor editors.
    /// </summary>
    public static bool RequestConstantRepaint = false;

    /// <summary>
    /// Draw supported member GUI for given targets. This method supports
    /// non-UnityEngine.Object instances, such as pure Serializable classes,
    /// that are part of <paramref name="targets"/>. <paramref name="getChildCallback"/>
    /// is called to access these serializable objects. If <paramref name="getChildCallback"/>
    /// is null, targets will be rendered.
    /// </summary>
    /// <param name="targets">Target UnityEngine.Object instances (used for Undo and SetDirty).</param>
    /// <param name="getChildCallback">Null and targets will be rendered, otherwise the returned
    ///                                instance from this callback.</param>
    public static void DrawMembersGUI( Object[] targets,
                                       Func<Object, object> getChildCallback = null,
                                       SerializedObject fallback = null )
    {
      targets = targets.Where( obj => obj != null ).ToArray();

      if ( targets.Length == 0 )
        return;

      var objects = targets.Select( target => getChildCallback == null ?
                                      target :
                                      getChildCallback( target ) )
                           .Where( obj => obj != null ).ToArray();
      if ( objects.Length == 0 )
        return;

      Undo.RecordObjects( targets, "Inspector" );

      var hasChanges = false;
      InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( objects[ 0 ].GetType() );
      var group = InspectorGroupHandler.Create();
      foreach ( InvokeWrapper wrapper in fieldsAndProperties ) {
        if ( !ShouldBeShownInInspector( wrapper.Member ) )
          continue;

        group.Update( wrapper, objects[ 0 ] );

        if ( group.IsHidden )
          continue;

        hasChanges = HandleType( wrapper, objects, fallback ) || hasChanges;
      }
      group.Dispose();

      if ( hasChanges ) {
        foreach ( var obj in targets )
          EditorUtility.SetDirty( obj );
      }
    }

    public static bool ShouldBeShownInInspector( MemberInfo memberInfo )
    {
      if ( memberInfo == null )
        return false;

      // Override hidden in inspector.
      if ( memberInfo.IsDefined( typeof( HideInInspector ), true ) )
        return false;

      // In general, don't show UnityEngine objects unless ShowInInspector is set.
      bool show = memberInfo.IsDefined( typeof( ShowInInspector ), true ) ||
                  !( memberInfo.DeclaringType.Namespace != null &&
                     memberInfo.DeclaringType.Namespace.Contains( "UnityEngine" ) );

      return show;
    }

    public sealed override void OnInspectorGUI()
    {
      if ( Utils.KeyHandler.HandleDetectKeyOnGUI( this.targets, Event.current ) )
        return;

      GUILayout.BeginVertical();

      ToolManager.OnPreTargetMembers( this.targets );

      DrawMembersGUI( this.targets, null, serializedObject );

      ToolManager.OnPostTargetMembers( this.targets );

      GUILayout.EndVertical();
    }

    public override bool RequiresConstantRepaint()
    {
      return base.RequiresConstantRepaint() || RequestConstantRepaint;
    }

    private void OnEnable()
    {
      if ( this.target == null )
        return;

      // Entire class/component marked as hidden - enable "hide in inspector".
      if ( this.target.GetType().GetCustomAttributes( typeof( HideInInspector ), false ).Length > 0 )
        this.target.hideFlags |= HideFlags.HideInInspector;

      ToolManager.OnTargetEditorEnable( this.targets );
    }

    private void OnDisable()
    {
      if ( this.target == null )
        Manager.OnEditorTargetsDeleted();

      ToolManager.OnTargetEditorDisable( this.targets );
    }

    private static bool HandleType( InvokeWrapper wrapper,
                                    object[] objects,
                                    SerializedObject fallback )
    {
      if ( !wrapper.CanRead() )
        return false;

      var drawerInfo = InvokeWrapperInspectorDrawer.GetDrawerMethod( wrapper.GetContainingType() );

      if ( wrapper.HasAttribute<InspectorSeparatorAttribute>() )
        Utils.GUI.SeparatorSimple();

      EditorGUI.showMixedValue = !wrapper.AreValuesEqual( objects );

      object value = null;
      bool changed = false;
      if ( drawerInfo.IsValid ) {
        value   = drawerInfo.Drawer.Invoke( null, new object[] { objects[ 0 ], wrapper } );
        changed = UnityEngine.GUI.changed &&
                  ( drawerInfo.IsNullable || value != null );
      }
      else if ( fallback != null ) {
        var serializedProperty = fallback.FindProperty( wrapper.Member.Name );
        if ( serializedProperty == null && wrapper.Member.Name.Length > 2 ) {
          var fieldName = "m_" + char.ToLower( wrapper.Member.Name[ 0 ] ) + wrapper.Member.Name.Substring( 1 );
          var fieldSerializedProperty = fallback.FindProperty( fieldName );
          // Unsure about synchronization if the serialized
          // field is found, i.e., if wrapper 'set' should be
          // invoked with changes.
          if ( fieldSerializedProperty != null && wrapper.GetContainingType().Name == fieldSerializedProperty.type )
            EditorGUILayout.PropertyField( fieldSerializedProperty );
        }
        if ( serializedProperty != null )
          EditorGUILayout.PropertyField( serializedProperty );
      }

      // Reset changed state so that non-edited values
      // are propagated to other properties.
      UnityEngine.GUI.changed = false;

      EditorGUI.showMixedValue = false;

      if ( !changed )
        return false;

      foreach ( var obj in objects ) {
        object newValue = value;
        if ( drawerInfo.IsValid && drawerInfo.CopyOp != null ) {
          newValue = wrapper.GetValue( obj );
          // CopyOp returns the new value for value types.
          var ret = drawerInfo.CopyOp.Invoke( null, new object[] { value, newValue } );
          if ( ret != null )
            newValue = ret;
        }
        wrapper.ConditionalSet( obj, newValue );
      }

      return true;
    }
  }
}
