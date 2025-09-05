FollowTheWay - Peak Climbing Mod

Advanced climb recording and sharing mod for Peak game, featuring integration with followtheway.ru server.

🎯 Features

Advanced Climb Recording - High-precision tracking of climbing routes

Cloud Synchronization - Upload and download climbs from followtheway.ru

Anti-Cheat Detection - Built-in fly detection and validation systems

Smart Filtering - Search and filter climbs by difficulty, author, map, etc.

Statistics Tracking - Detailed climb analytics and leaderboards

Modern UI - Clean and intuitive mod menu interface

Data Compression - Efficient storage and transfer of climb data

🚀 Installation

Download the latest release from the releases page

Extract the mod files to your Peak game directory

Configure your API key (see Configuration section)

Launch Peak and enjoy!

⚙️ Configuration
API Key Setup

IMPORTANT: You need a valid API key from followtheway.ru to use this mod.

Get your API key from: https://followtheway.ru/api/v1/auth/keys

Set environment variable before building:

# Windows
set FOLLOWTHEWAY_API_KEY=your_api_key_here

# Linux/macOS
export FOLLOWTHEWAY_API_KEY=your_api_key_here

Build the mod using the provided build scripts
Custom Server (Optional)

To use a different server, set the server URL:

# Windows
set FOLLOWTHEWAY_SERVER_URL=https://your-server.com

# Linux/macOS
export FOLLOWTHEWAY_SERVER_URL=https://your-server.com

🔨 Building from Source
Prerequisites

.NET Framework 4.7.2 or later

Peak game installed

Valid API key from followtheway.ru

Build Steps
Clone the repository:
git clone https://github.com/Abbadon999/FollowTheWay-Peak.git
cd FollowTheWay-Peak

Set your API key:
# Windows
set FOLLOWTHEWAY_API_KEY=your_api_key_here

# Linux/macOS
export FOLLOWTHEWAY_API_KEY=your_api_key_here

Run build script:
# Windows
build-scripts\build.bat

# Linux/macOS
chmod +x build-scripts/build.sh
./build-scripts/build.sh

Find the compiled mod in bin/Release/
🔒 Security

API keys are never stored in source code

ApiKeys.cs is automatically generated during build

Template files are safe to commit to version control

Never share your API key or commit ApiKeys.cs

🎮 Usage
Recording Climbs

Start climbing in Peak

Press F1 to open the FollowTheWay menu

Click "Start Recording" to begin tracking your climb

Complete your climb and press "Stop Recording"

Upload to server or save locally

Downloading Climbs

Open the mod menu (F1)

Go to "Browse Climbs" tab

Search or filter climbs by your preferences

Click "Download" to get the climb data

Click "Visualize" to see the route in-game

Filters and Search

Search by title or author name

Filter by difficulty (Easy, Medium, Hard, Very Hard, Extreme)

Filter by map or biome

Sort by date, popularity, or difficulty

View statistics for each climb

🛡️ Anti-Cheat Features

Fly Detection - Automatically detects impossible movements

Speed Validation - Flags unrealistic climbing speeds

Route Analysis - Validates climb paths for authenticity

Data Integrity - Ensures climb data hasn't been tampered with

📊 Statistics

Track your climbing progress with detailed statistics:

Total climbs completed

Average climb difficulty

Total distance climbed

Personal best times

Favorite climbing areas

Achievement progress

🔧 API Integration

This mod integrates with the followtheway.ru API:

Base URL: https://followtheway.ru/api/v1

Authentication: API Key based

Endpoints: Climbs, Statistics, Health checks

Data Format: JSON with binary payload compression

🤝 Contributing

Fork the repository

Create a feature branch

Make your changes

Test thoroughly

Submit a pull request

Development Guidelines

Follow C# coding standards

Add unit tests for new features

Update documentation

Never commit API keys or sensitive data

Use the provided build scripts

📝 Changelog
v0.0.1 (Initial Release)

Basic climb recording and playback

Integration with followtheway.ru API

Anti-cheat detection system

Modern UI with filtering and search

Data compression and optimization

Cross-platform build scripts

🐛 Known Issues
None currently reported
📞 Support

Issues: Report bugs on GitHub Issues

Discord: Join our community server

Email: support@followtheway.ru

API Documentation: https://followtheway.ru/docs

📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

🙏 Acknowledgments

Peak Game Developers - For creating an amazing climbing game

BepInEx Team - For the excellent modding framework

Community Contributors - For testing and feedback

Original FollowMe Mod - For inspiration and reference

Made with ❤️ by ABBADON

Climb higher, share further, follow the way!