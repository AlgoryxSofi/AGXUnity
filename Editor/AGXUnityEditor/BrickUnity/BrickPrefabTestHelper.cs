using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.IO;

using AGXUnity.BrickUnity;
using AGXUnity.Utils;
using AGXUnity.BrickUnity.Factories;
using B_Node = Brick.Scene.Node;
using B_Component = Brick.Physics.Component;
using B_RigidBody = Brick.Physics.Mechanics.RigidBody;
using B_Connector = Brick.Physics.Mechanics.AttachmentPairConnector;
using B_MultiConnector = Brick.Physics.Mechanics.MultiAttachmentConnector;
using B_Geometry = Brick.Physics.Geometry;
using B_Visual = Brick.Visual;
using B_TwoBodyTire = Brick.AGXBrick.TwoBodyTire;
using B_Joint = Brick.Robotics.Joint;
using B_Camera = Brick.Scene.Camera;
using System.Linq;
using AGXUnityEditor.IO;

namespace AGXUnityEditor.BrickUnity
{
  public class BrickPrefabTestHelper
  {

    private bool ValidateTransform(AGXUnity.RigidBody au_body, agx.RigidBody agx_body) {
      var diff_p = au_body.transform.position.ToHandedVec3().distance(agx_body.getPosition());
      var diff_q = (au_body.transform.rotation.ToHandedQuat() - agx_body.getRotation()).length();
      return diff_p < 1E-7 && diff_q < 1E-7;
        
    }
    public void CreateAndCompareBrickSimulationWithGameObject(GameObject go_createdFromBrick, string filepath, string modelName) {
      var b_component = BrickUtils.LoadComponentFromFile(filepath, modelName);
      var b_simulation = new Brick.AGXBrick.BrickSimulation();
      b_simulation.AddComponent(b_component);
      
      var bodies = Object.FindObjectsOfType<AGXUnity.RigidBody>();
      agx.RigidBody agxBodyWithCorrectName = null;
      foreach(var body in bodies) {
        var found = false;
        foreach(var agxBody in b_simulation.AgxSimulation.getRigidBodies()) {
          if (agxBody.getName() != body.name)
            continue;
          agxBodyWithCorrectName = agxBody.get();
          if (!ValidateTransform(body, agxBody.get()))
            continue;
          found = true;
          break;
        }
        if (!found) {
          var bodyName = "";
          if (agxBodyWithCorrectName != null)
            bodyName = agxBodyWithCorrectName.getName();
          agx.Vec3 posAUv = body.transform.position.ToHandedVec3();
          agx.Vec3 posAv = agxBodyWithCorrectName.getPosition();
          var posAU = posAUv.x().ToString("0.0000000") + " " + posAUv.y().ToString("0.0000000") + posAUv.z().ToString("0.0000000");
          var posA = posAv.x().ToString("0.0000000") + " " + posAv.y().ToString("0.0000000") + posAv.z().ToString("0.0000000");
          throw new System.Exception("No matching AGX body found to AGXUnity body. Name: " + bodyName + " p: " + posAU + " p: " + posA);
        }
      }
      
      return;
    }

    /// <summary>
    /// Import a Brick file into a prefab GameObject.
    /// </summary>
    /// <param name="filepath">Path to the Brick file.</param>
    /// <param name="modelName">Name of the model in the Brick file.</param>
    /// <returns>A reference to the created GameObject.</returns>
    




    // Recursively create GameObjects with corresponding AGXUnity components from a Brick.Node and its children
    private void CompareUnityAndBrickObjects(
      Brick.AGXBrick.BrickSimulation b_simulation,
      GameObject go
      )
    {
      
     

      
    }

    private void VerifyRigidBody(Brick.AGXBrick.BrickSimulation b_simulation, AGXUnity.RigidBody au_body)
    {
      
    }

  
  }
}
