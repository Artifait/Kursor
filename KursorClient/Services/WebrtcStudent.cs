using Microsoft.MixedReality.WebRTC;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace KursorClient.Services
{
    public class WebrtcStudent : IDisposable
    {
        private readonly PeerConnection _pc;
        private DataChannel? _dc;
        private readonly SignalRService _signalR;
        private readonly string _token;
        private readonly Action<double, double> _onCoords;
        private bool _initialized = false;

        public WebrtcStudent(SignalRService signalR, string token, Action<double, double> onCoords)
        {
            _signalR = signalR;
            _token = token;
            _onCoords = onCoords;
            _pc = new PeerConnection();
            _signalR.OfferReceived += OnOfferReceived;
            _signalR.IceCandidateReceived += OnRemoteIce;
        }

        private async void OnOfferReceived(string sdp)
        {
            try
            {
                if (!_initialized) await InitializeAsync();

                // set remote description (offer)
                var offer = new SdpMessage { Type = SdpMessageType.Offer, Content = sdp };
                await _pc.SetRemoteDescriptionAsync(offer);

                _pc.CreateAnswer(); 
            }
            catch { }
        }


        public async Task InitializeAsync()
        {
            if (_initialized) return;

            var pcConfig = new PeerConnectionConfiguration();
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

            _pc.DataChannelAdded += (dc) =>
            {
                _dc = dc;
                _dc.MessageReceived += OnDataChannelMessage;
            };

            _initialized = true;
        }

        private void OnRemoteIce(IceCandidate candidate)
        {
            try { _pc.AddIceCandidate(candidate); } catch { }
        }

        private void OnDataChannelMessage(byte[] data)
        {
            try
            {
                int offset = (data.Length == 5) ? 1 : 0;
                if (data.Length != 4 && data.Length != 5) return;
                ushort qx = (ushort)((data[offset] << 8) | data[offset + 1]);
                ushort qy = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
                double nx = qx / 65535.0;
                double ny = qy / 65535.0;
                Application.Current.Dispatcher.Invoke(() => _onCoords(nx, ny));
            }
            catch { }
        }

        public void Dispose()
        {
            try
            {
                _signalR.OfferReceived -= OnOfferReceived;
                _signalR.IceCandidateReceived -= OnRemoteIce;
            }
            catch { }
            try { _pc?.Close(); _pc?.Dispose(); } catch { }
        }
    }
}
