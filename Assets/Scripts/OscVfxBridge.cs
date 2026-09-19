using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.VFX;

namespace OpticalFlowTest
{
    [AddComponentMenu("Abysses/OSC VFX Bridge")]
    public sealed class OscVfxBridge : MonoBehaviour
    {
        public enum PropertyType { Float, Int, Bool, Vector2, Vector3, Vector4 }

        [Serializable]
        public sealed class Binding
        {
            public bool enabled = true;
            public string address = "/vfx/flow";
            [Min(0)] public int firstArgument;
            [Tooltip("Optional override of the bridge's default VFX.")]
            public VisualEffect target;
            public string property = "FlowFactor";
            public PropertyType type;
            public float multiplier = 1;
            public float offset;
            public bool clamp;
            public Vector2 limits = new Vector2(0, 1);
            [Tooltip("Latest received arguments, even when this binding is disabled.")]
            public float[] lastReceivedValues;
            [NonSerialized] public string warning;

            public void Apply(VisualEffect fallback, float[] values, UnityEngine.Object context)
            {
                var vfx = target != null ? target : fallback;
                int count = type == PropertyType.Vector2 ? 2 : type == PropertyType.Vector3 ? 3 : type == PropertyType.Vector4 ? 4 : 1;
                string error = null;
                if (vfx == null || string.IsNullOrEmpty(property)) error = "Missing VFX target or property.";
                else if (firstArgument < 0 || firstArgument > values.Length - count) error = "Not enough OSC arguments.";
                else
                {
                    bool valid = type == PropertyType.Float ? vfx.HasFloat(property) :
                        type == PropertyType.Int ? vfx.HasInt(property) :
                        type == PropertyType.Bool ? vfx.HasBool(property) :
                        type == PropertyType.Vector2 ? vfx.HasVector2(property) :
                        type == PropertyType.Vector3 ? vfx.HasVector3(property) : vfx.HasVector4(property);
                    if (!valid) error = "VFX property is missing or has a different type.";
                }
                if (error != null)
                {
                    if (warning != error) Debug.LogWarning($"OSC {address} -> {property}: {error}", context);
                    warning = error;
                    return;
                }
                warning = null;
                Vector4 value = Vector4.zero;
                for (int i = 0; i < count; i++)
                {
                    float x = values[firstArgument + i] * multiplier + offset;
                    if (float.IsNaN(x) || float.IsInfinity(x)) return;
                    value[i] = clamp ? Mathf.Clamp(x, Mathf.Min(limits.x, limits.y), Mathf.Max(limits.x, limits.y)) : x;
                }
                switch (type)
                {
                    case PropertyType.Float: vfx.SetFloat(property, value.x); break;
                    case PropertyType.Int:
                        vfx.SetInt(property, (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Round((double)value.x)))); break;
                    case PropertyType.Bool: vfx.SetBool(property, value.x != 0); break;
                    case PropertyType.Vector2: vfx.SetVector2(property, new Vector2(value.x, value.y)); break;
                    case PropertyType.Vector3: vfx.SetVector3(property, new Vector3(value.x, value.y, value.z)); break;
                    case PropertyType.Vector4: vfx.SetVector4(property, value); break;
                }
            }
        }

        [Header("Receive (IPv4 / UDP)")]
        [Range(1, 65535)] public int listenPort = 9000;
        [Min(1), Tooltip("Limits receive work per frame.")]
        public int maxMessagesPerFrame = 128;
        public VisualEffect defaultTarget;
        public List<Binding> bindings = new List<Binding>();

        [Header("Optional sending")]
        public bool allowSending;
        public string remoteAddress = "127.0.0.1";
        [Range(1, 65535)] public int remotePort = 9001;

        [Header("Runtime diagnostics")]
        [SerializeField] bool listening;
        [SerializeField] int receivedMessages;
        [SerializeField] int rejectedMessages;
        [SerializeField] string lastAddress;
        [SerializeField] float[] lastValues;

        UdpClient receiver;
        UdpClient sender;

        void OnEnable()
        {
            try
            {
                receiver = new UdpClient(AddressFamily.InterNetwork);
                receiver.Client.ExclusiveAddressUse = true;
                receiver.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));
                receiver.Client.Blocking = false;
                listening = true;
            }
            catch (Exception e) when (e is SocketException || e is ArgumentException)
            {
                CloseSockets();
                Debug.LogError($"Cannot listen for OSC on UDP {listenPort}: {e.Message}", this);
            }
        }

        // All VFX calls happen on Unity's main thread, after ordinary Update controllers.
        void LateUpdate()
        {
            if (receiver == null) return;
            try
            {
                for (int i = 0; i < Mathf.Max(1, maxMessagesPerFrame); i++)
                {
                    if (!receiver.Client.Poll(0, SelectMode.SelectRead)) break;
                    IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    byte[] packet = receiver.Receive(ref source);
                    if (!OscMessageCodec.TryDecode(packet, out string address, out float[] values))
                    {
                        rejectedMessages++;
                        continue;
                    }
                    receivedMessages++;
                    lastAddress = address;
                    lastValues = values;
                    foreach (var binding in bindings)
                        if (binding != null && binding.address == address)
                        {
                            binding.lastReceivedValues = values;
                            if (binding.enabled) binding.Apply(defaultTarget, values, this);
                        }
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.WouldBlock) return;
                CloseSockets();
                Debug.LogError($"OSC receive stopped: {e.Message}", this);
            }
        }

        /// <summary>Call on Unity's main thread. Destination is configured in the Inspector.</summary>
        public bool Send(string address, params float[] values)
        {
            if (!isActiveAndEnabled || !allowSending) return false;
            try
            {
                if (!IPAddress.TryParse(remoteAddress, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                    throw new ArgumentException("Remote Address must be an IPv4 address.");
                var destination = new IPEndPoint(ip, remotePort);
                byte[] packet = OscMessageCodec.Encode(address, values);
                if (sender == null) sender = new UdpClient(AddressFamily.InterNetwork);
                sender.Send(packet, packet.Length, destination);
                return true;
            }
            catch (Exception e) when (e is SocketException || e is ArgumentException)
            {
                Debug.LogWarning($"OSC send failed: {e.Message}", this);
                return false;
            }
        }

        void OnDisable() => CloseSockets();
        void OnDestroy() => CloseSockets();

        void CloseSockets()
        {
            receiver?.Close();
            sender?.Close();
            receiver = null;
            sender = null;
            listening = false;
        }
    }
}
