using UnityEngine;
using Klak.TestTools;

namespace OpticalFlowTest {

public sealed class OpticalFlowVisualizer : MonoBehaviour
{
    [SerializeField] ImageSource _source = null;
    [SerializeField] OpticalFlowEstimator _estimator = null;

    [Header("Display Options")]
    [SerializeField, Tooltip("Affiche ou masque le flux de la webcam")]
    bool _showWebcamFeed = true;

    [SerializeField, Tooltip("Inverse horizontalement l'affichage de la webcam")]
    bool _flipWebcamHorizontally = true;

    [HideInInspector, SerializeField] Mesh _mesh = null;
    [HideInInspector, SerializeField] Shader _shader = null;

    Material _material;
    Mesh _flippedMesh;

    void Start()
    {
        _material = new Material(_shader);

        // Use reversed UVs instead of a negative transform scale. A negative
        // scale also reverses triangle winding and can make the mesh get culled.
        _flippedMesh = Instantiate(_mesh);
        _flippedMesh.name = $"{_mesh.name} (Horizontally Flipped)";

        var uvs = _flippedMesh.uv;
        for (var i = 0; i < uvs.Length; i++)
            uvs[i].x = 1 - uvs[i].x;
        _flippedMesh.uv = uvs;
    }

    void OnDestroy()
    {
        Destroy(_material);
        Destroy(_flippedMesh);
    }

    void Update()
    {
        if (!_showWebcamFeed) return;

        _material.mainTexture = _source.AsTexture;
        _material.SetTexture("_FlowTex", _estimator.AsRenderTexture);

        Graphics.DrawMesh
          (_flipWebcamHorizontally ? _flippedMesh : _mesh,
           transform.localToWorldMatrix, _material, gameObject.layer);
    }
}

} // namespace OpticalFlowTest
