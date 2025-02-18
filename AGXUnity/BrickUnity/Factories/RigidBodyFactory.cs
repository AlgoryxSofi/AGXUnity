﻿using System;
using System.IO;
using UnityEngine;

using AGXUnity.Utils;

using B_RigidBody = Brick.Physics.Mechanics.RigidBody;
using B_Geometry = Brick.Physics.Geometry;

namespace AGXUnity.BrickUnity.Factories
{
  public static class RigidBodyFactory
  {
    public static AGXUnity.RigidBody AddRigidBody(this GameObject go, B_RigidBody b_body)
    {
      var au_body = go.AddComponent<AGXUnity.RigidBody>();
      au_body.MotionControl = b_body.MotionControl.ToAgxMotionControl();

      var mp3d = b_body.MassProperties as Brick.Physics.MassProperties.MassProperties3D;
      if (!mp3d._massIsDefault)
      {
        au_body.MassProperties.Mass.UserValue = Convert.ToSingle(b_body.Mass);
        au_body.MassProperties.Mass.UseDefault = false;
      }

      if (!mp3d._inertiaIsDefault)
      {
        au_body.MassProperties.InertiaDiagonal.UserValue = mp3d.Inertia.ToVector3();
        au_body.MassProperties.InertiaDiagonal.UseDefault = false;
      }

      if (!mp3d._localTransformIsDefault)
      {
        au_body.MassProperties.CenterOfMassOffset.UserValue = b_body.MassProperties.LocalPosition.ToHandedVector3();
        au_body.MassProperties.CenterOfMassOffset.UseDefault = false;
      }

      return au_body;
    }

    public static AGXUnity.Collide.Shape AddShape(this GameObject go, B_Geometry b_geometry)
    {
      AGXUnity.Collide.Shape au_shape;
      switch (b_geometry)
      {
        case B_Geometry.Box b_box:
          au_shape = go.AddBox(b_box);
          break;
        case B_Geometry.Sphere b_sphere:
          au_shape = go.AddSphere(b_sphere);
          break;
        case B_Geometry.Cylinder b_cylinder:
          au_shape = go.AddCylinder(b_cylinder);
          break;
        case B_Geometry.Trimesh b_triMesh:
          au_shape = go.AddMesh(b_triMesh);
          break;
        case B_Geometry.Plane b_plane:
          au_shape = go.AddPlane(b_plane);
          break;
        case B_Geometry.Capsule b_capsule:
          au_shape = go.AddCapsule(b_capsule);
          break;
        case B_Geometry.Rotated2DPolygonMesh b_r2DPMesh:
          au_shape = go.AddRotated2DPolygonMesh(b_r2DPMesh);
            break;
        case B_Geometry.HollowCylinder b_hollowCylinder:
            au_shape = go.AddHollowCylinder(b_hollowCylinder);
            break;
        default:
          Debug.LogError($"Cannot create Shape for Brick object {b_geometry}. Unsupported type: {b_geometry.GetType()}");
          return null;
      }
      au_shape.CollisionsEnabled = b_geometry.EnableCollisions;

      // Make sure to render shape if it has a render material
      if (b_geometry.RenderMaterial != null)
      {
        var b_material = b_geometry.RenderMaterial;
        au_shape.AddGeometryRendering(b_material);
      }
      return au_shape;
    }


    // Add rendering to a AGX for Unity shape using the render material from Brick
    public static void AddGeometryRendering(this Collide.Shape au_shape, B_Geometry.RenderData b_material)
    {
      var go_visual = Rendering.ShapeVisual.Create(au_shape);
      foreach (var renderer in go_visual.GetComponentsInChildren<MeshRenderer>())
      {
        renderer.sharedMaterial = new Material(Shader.Find("Standard"));

        var color = b_material.DiffuseColor.ToUnityColor();
        color.a = (float)b_material.Opacity;
        renderer.sharedMaterial.SetVector("_Color", color);

        renderer.sharedMaterial.SetVector("_EmissionColor", b_material.EmissiveColor.ToUnityColor());

        renderer.sharedMaterial.SetFloat("_Metallic", 0.3f);
        renderer.sharedMaterial.SetFloat("_Glossiness", 0.8f);

        if (b_material.Opacity < 1.0f)
          renderer.sharedMaterial.SetBlendMode(Rendering.BlendMode.Transparent);

        if (!b_material._textureIsDefault)
        {
          var pngBytes = File.ReadAllBytes(b_material.AbsoluteTextureFilepath);
          var tex = new Texture2D(1, 1);
          tex.LoadImage(pngBytes);
          renderer.sharedMaterial.SetTexture("_MainTex", tex);
        }
      }
    }


    public static AGXUnity.Collide.Sphere AddSphere(this GameObject go, B_Geometry.Sphere b_sphere)
    {
      var au_sphere = go.AddComponent<AGXUnity.Collide.Sphere>();
      au_sphere.Radius = (float)b_sphere.Radius;
      return au_sphere;
    }

    public static AGXUnity.Collide.Box AddBox(this GameObject go, B_Geometry.Box b_box)
    {
      var au_box = go.AddComponent<AGXUnity.Collide.Box>();
      au_box.HalfExtents = (b_box.Lengths / 2.0).ToVector3();
      return au_box;
    }

    public static AGXUnity.Collide.Cylinder AddCylinder(this GameObject go, B_Geometry.Cylinder b_cylinder)
    {
      var au_cylinder = go.AddComponent<AGXUnity.Collide.Cylinder>();
      au_cylinder.Radius = (float)b_cylinder.Radius;
      au_cylinder.Height = (float)b_cylinder.Length;
      return au_cylinder;
    }
    public static AGXUnity.Collide.Capsule AddCapsule(this GameObject go, B_Geometry.Capsule b_capsule)
    {
      var au_capsule = go.AddComponent<AGXUnity.Collide.Capsule>();
      au_capsule.Radius = (float)b_capsule.Radius;
      au_capsule.Height = (float)b_capsule.Length;
      return au_capsule;
    }

    public static AGXUnity.Collide.HollowCylinder AddHollowCylinder(this GameObject go, B_Geometry.HollowCylinder b_hollowCylinder)
    {
      var au_hollowCylinder = go.AddComponent<AGXUnity.Collide.HollowCylinder>();
      au_hollowCylinder.Thickness = (float)b_hollowCylinder.Thickness;
      au_hollowCylinder.Radius = (float)b_hollowCylinder.InnerRadius;
      au_hollowCylinder.Height = (float)b_hollowCylinder.Length;
      return au_hollowCylinder;
    }

    public static AGXUnity.Collide.Mesh AddMesh(this GameObject go, B_Geometry.Trimesh b_triMesh)
    {
      var au_mesh = go.AddComponent<AGXUnity.Collide.Mesh>();
      // Errors can occur while reading trimesh. For example if file not found.
      b_triMesh.InitTask.Wait();

      // Copy vertices and indices into agx Vectors for processing in
      // AGXUnity.Utils.MeshSplitter as is done in
      // AGXUnityEditor.IO.InputAGXFile
      // Can this be done avioded? If Brick already provided these. Or
      // if MeshSplitter could take a list of agx.Vec3 instead of an agx.Vector?
      var agxVertices = new agx.Vec3Vector();
      var agxIndices = new agx.UInt32Vector();
      foreach (var v in b_triMesh.Vertices)
      {
        agxVertices.Add(v);
      }
      foreach (var i in b_triMesh.Indices)
      {
        agxIndices.Add((uint)i);
      }

      // Create Unity meshes with AGXUnitys utility MeshSplitter
      var meshes = MeshSplitter.Split(
                                        agxVertices,
                                        agxIndices,
                                        v => v.ToHandedVector3() * (float)b_triMesh.Scale
                                        ).Meshes;
      au_mesh.SetSourceObject(null);
      foreach (var mesh in meshes)
      {
        au_mesh.AddSourceObject(mesh);
      }
      return au_mesh;
    }
    public static AGXUnity.Collide.Mesh AddRotated2DPolygonMesh(this GameObject go, B_Geometry.Rotated2DPolygonMesh b_r2DPMesh)
    {
      var au_mesh = go.AddComponent<AGXUnity.Collide.Mesh>();

      // Copy vertices and indices into agx Vectors for processing in
      // AGXUnity.Utils.MeshSplitter as is done in
      // AGXUnityEditor.IO.InputAGXFile
      // Can this be done avioded? If Brick already provided these. Or
      // if MeshSplitter could take a list of agx.Vec3 instead of an agx.Vector?
      var agxVertices = new agx.Vec3Vector();
      var agxIndices = new agx.UInt32Vector();
      foreach (var v in b_r2DPMesh.Vertices)
      {
        agxVertices.Add(v);
      }
      foreach (var i in b_r2DPMesh.Indices)
      {
        agxIndices.Add((uint)i);
      }

      // Create Unity meshes with AGXUnitys utility MeshSplitter
      var meshes = MeshSplitter.Split(
                                        agxVertices,
                                        agxIndices,
                                        v => v.ToHandedVector3() * (float)b_r2DPMesh.Scale
                                        ).Meshes;
      au_mesh.SetSourceObject(null);
      foreach (var mesh in meshes)
      {
        au_mesh.AddSourceObject(mesh);
      }
      return au_mesh;
    }


    public static void SetPlaneEquationTransform(this GameObject go, Brick.Math.Vec3 b_normal, double b_d)
    {
      // Set the transform according to the plane equation
      var normal = b_normal.ToHandedVector3();
      Quaternion rotation = new Quaternion();
      rotation.SetFromToRotation(go.transform.up, normal);
      var distance = (float)b_d / normal.magnitude;

      // Rotate transforms up vector to point along
      go.transform.localRotation = rotation;
      go.transform.localPosition = -normal.normalized * distance;
    }

    public static AGXUnity.Collide.Plane AddPlane(this GameObject go, B_Geometry.Plane b_plane)
    {
      GameObject planeEquation = new GameObject("PlaneEquation");
      planeEquation.transform.parent = go.transform;

      var au_plane = planeEquation.AddComponent<AGXUnity.Collide.Plane>();

      var b_normal = new Brick.Math.Vec3(b_plane.A, b_plane.B, b_plane.C);
      planeEquation.SetPlaneEquationTransform(b_normal, b_plane.D);

      return au_plane;
    }
  }
}