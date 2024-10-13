# ReconTransform

Attempts to synchronize physics-based objects in unity.

WARNING: Do not use for competitive games. This package is to be used in casual co-op games where enemies and other physics objects need to be just close enough between client instances.


## Setup
- Have unity 2022.3.20f1 installed
- Package manager > Install netcode for game objects from unity registry
- Package manager > add git repo for Steamworks using https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.facepunch 

## Running the Scene
- Have two steam accounts on different computers
- Host on one computer, join via steam friends list on other computer
- Shoot with [LMB] or press [Space] to apply networked knockback
- observe ReconTransform doing its work to sync position across clients.