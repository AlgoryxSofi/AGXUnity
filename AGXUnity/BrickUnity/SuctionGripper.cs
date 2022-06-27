using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity;
using AGXUnity.BrickUnity;
using Brick;
using AGXUnity.Utils;

public class SuctionGripper : MonoBehaviour
{
  public float updateTime = 0.02f;
  public agxModel.SuctionGripper agx_suctionGripper;
  private Brick.AGXBrick.SuctionGripper b_suctionGripper;
  private List<GameObject> cupObjects = new List<GameObject>();
  private List<AGXUnity.Collide.Mesh> cupMeshes = new List<AGXUnity.Collide.Mesh>();

  private double defaultVacuum;

  private float lastUpdate = 0f;

  private void UpdateMeshes()
  {
    foreach (var cupMesh in cupMeshes)
    {
      var m = cupMesh.Native;

      var meshes = MeshSplitter.Split(
                                        m.getMeshData().getVertices(),
                                        m.getMeshData().getIndices(),
                                        v => v.ToHandedVector3(),
                                        UInt16.MaxValue
                                        ).Meshes;
      var sourceMesh = cupMesh.SourceObjects;

      if (sourceMesh.Length == meshes.Length)
      {
        int i = 0;
        foreach (var mesh in meshes)
        {
          sourceMesh[i].Clear();
          sourceMesh[i].SetVertices(mesh.vertices);
          sourceMesh[i].triangles = mesh.triangles;
          sourceMesh[i].uv = mesh.uv;
          sourceMesh[i].SetNormals(mesh.normals);
          i++;
        }
      }
      /*cupMesh.SetSourceObject(null);
      foreach (var mesh in meshes)
      {
        cupMesh.AddSourceObject(mesh);
      }*/
    }
  }


  public void InitSuctionGripper(Brick.AGXBrick.SuctionGripper brickSuctionGripper, agxModel.SuctionGripper suctionGripper)
  {
    agx_suctionGripper = suctionGripper;
    b_suctionGripper = brickSuctionGripper;

    var brickObjects = GetComponentsInChildren<BrickObject>();

    foreach (var obj in brickObjects)
    {
      if (obj.type == "Brick.AGXBrick.SuctionGripper.Cup")
      {
        cupObjects.Add(obj.gameObject);
        cupMeshes.Add(obj.GetComponentInChildren<AGXUnity.Collide.Mesh>());
      }
    }

    defaultVacuum = b_suctionGripper.Vacuum.DesiredVacuum;
    UpdateMeshes();
    SetPumpEnabled(false);
  }

  public void SetPumpVacuumScaing(double value)
  {
    b_suctionGripper.Vacuum.DesiredVacuum = defaultVacuum*value;
    agx_suctionGripper.getVacuumSystem().setDesiredVacuum(defaultVacuum * value);
  }

  public void SetPumpEnabled(bool enabled)
  {
    b_suctionGripper.Vacuum.EnablePump = enabled;
  }

  // Update is called once per frame
  void FixedUpdate()
  {
    if (Time.time - lastUpdate >= updateTime)
    {
      UpdateMeshes();
      lastUpdate = Time.time;
    }

  }
}
