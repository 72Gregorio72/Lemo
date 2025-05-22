# Lemovie - VR Media Experience Platform

## Overview
Lemovie is a Virtual Reality media platform built with Unity that provides an immersive way to experience video and audio content. The application features an intuitive carousel-based interface, controller visualization, and interactive rating system, all designed to create a seamless VR media viewing experience.

## Features

### Media Playback
- 360° video playback support
- Audio playback capabilities
- Seamless media switching and transitions
- Pause/Play functionality for both video and audio content

### User Interface
- Intuitive carousel-based content navigation
- Two UI modes:
  - 360° UI for immersive content browsing
  - Scene UI for traditional content layout
- Interactive button visualization on VR controllers
- Dynamic visual feedback for user interactions

### Rating System
- In-VR rating interface for media content
- Persistent rating storage
- Average rating calculation and display
- User-friendly rating popup at comfortable viewing distance

### Controller Integration
- Full VR controller support
- Visual feedback for button presses
- Custom button mapping for media control
- Support for both left and right controllers

### Interaction Features
- Hand tracking support
- Poke interaction for UI elements
- Gesture recognition
- Smooth transitions and animations

## Technical Requirements

### Software Requirements
- Unity 2022.3 or later
- XR Interaction Toolkit 3.1.1
- Universal Render Pipeline (URP)

### Hardware Requirements
- VR-ready PC/laptop
- Compatible VR headset (Meta Quest, etc.)
- VR controllers

## Project Structure

```
Lemovie/
├── Assets/
│   ├── Script/                 # Core application scripts
│   ├── VRTemplateAssets/       # VR-specific templates and assets
│   └── Samples/               # XR Interaction Toolkit samples
├── Packages/                   # Unity package dependencies
└── ProjectSettings/           # Unity project settings
```

### Key Components

#### XRButtonVisualFeedback
Handles visual feedback for VR controller buttons, including:
- Button state visualization
- Custom sprite support
- Material and outline effects
- Dynamic color changes

#### XRCarouselInputController
Manages the carousel-based UI system with features like:
- Content navigation
- Media playback control
- Rating system integration
- Input handling for VR controllers

## Setup and Installation

1. Clone the repository
2. Open the project in Unity 2022.3 or later
3. Ensure all required packages are installed through the Package Manager
4. Configure your VR hardware in Unity's XR Plugin Management
5. Build and deploy to your VR device

## Development Guidelines

### Adding New Content
1. Place media files in the appropriate content directory
2. Update the carousel data to include new content
3. Ensure proper meta-data is included for the rating system

### Modifying UI Elements
1. Use the provided prefabs for consistency
2. Follow the established visual feedback patterns
3. Test interactions in both UI modes (360° and Scene)

### Controller Customization
1. Modify the XRButtonVisualFeedback script for new button behaviors
2. Update button sprites and materials as needed
3. Test feedback with various controller states

## Contributing
Contributions are welcome! Please follow these steps:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request with a clear description of changes

## License
[Insert License Information]

## Contact
[Insert Contact Information] 