using Microsoft.MixedReality.WebRTC;

namespace KursorClient.Services
{
    public class WebrtcTeacher : IDisposable
    {
        private readonly PeerConnection _pc;
        private DataChannel? _dc;
        private readonly SignalRService _signalR;
        private readonly string _token;
        private bool _initialized = false;

        public bool DataChannelOpen => _dc != null && _dc.State == DataChannel.ChannelState.Open;

        public WebrtcTeacher(SignalRService signalR, string token)
        {
            _signalR = signalR;
            _token = token;
            _pc = new PeerConnection();
            _signalR.AnswerReceived += OnAnswerReceived;
            _signalR.IceCandidateReceived += OnRemoteIce;
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            var pcConfig = new PeerConnectionConfiguration(); // no ICE servers by default

            await _pc.InitializeAsync(pcConfig);

            _pc.LocalSdpReadytoSend += (sdp) =>
            {
                if (sdp.Type == SdpMessageType.Offer) _ = _signalR.SendOffer(_token, sdp.Content);
                else if (sdp.Type == SdpMessageType.Answer) _ = _signalR.SendAnswer(_token, sdp.Content);
            };

            _pc.IceCandidateReadytoSend += (candidate) =>
            {
                _ = _signalR.SendIceCandidate(_token, candidate.Content);
            };

            _dc = await _pc.AddDataChannelAsync(label: "Kursor", ordered: false, reliable: false);
            _dc.MessageReceived += OnMessageReceived;
            _dc.StateChanged += () => { /* optional */ };

            _pc.CreateOffer(); 

            _initialized = true;
        }

        private async void OnAnswerReceived(string sdp)
        {
            try
            {
                var desc = new SdpMessage { Type = SdpMessageType.Answer, Content = sdp };
                await _pc.SetRemoteDescriptionAsync(desc);
            }
            catch { }
        }

        private void OnRemoteIce(IceCandidate candidate)
        {
            try { _pc.AddIceCandidate(candidate); }
            catch { }
        }

        private void OnMessageReceived(byte[] data) { /* no-op for teacher */ }

        public void SendCoords(float nx, float ny)
        {
            if (!DataChannelOpen) return;
            ushort qx = (ushort)Math.Clamp((int)(nx * 65535f), 0, 65535);
            ushort qy = (ushort)Math.Clamp((int)(ny * 65535f), 0, 65535);
            var buffer = new byte[4];
            buffer[0] = (byte)(qx >> 8);
            buffer[1] = (byte)(qx & 0xFF);
            buffer[2] = (byte)(qy >> 8);
            buffer[3] = (byte)(qy & 0xFF);
            try { _dc!.SendMessage(buffer); } catch { }
        }

        public void Dispose()
        {
            try
            {
                _signalR.AnswerReceived -= OnAnswerReceived;
                _signalR.IceCandidateReceived -= OnRemoteIce;
            }
            catch { }
            try { _pc?.Close(); _pc?.Dispose(); } catch { }
        }
    }
}
