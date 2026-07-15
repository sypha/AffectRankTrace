# AffectRankTrace

A tool for affect annotation, combining RankTrace (Lopes et al. 2017) and Self-Assessment Manikins (SAM) (Bradley and Lang 1994) methods.

## Overview

AffectRankTrace is an annotation tool that enables continuous, unbounded annotation of one affective dimension in real time, and discrete annotation of the three PAD dimensions. Continuous annotation, similar to RankTrace, allows users to control a curve in real time to assess the intensity of one emotional dimension. Discrete annotation is facilitated via a digitalized version of a 7-point scale Self-Assessment Manikin (SAM). The pleasure, arousal, and dominance scales are designed as slider controls ranging from -3 to 3, with brief descriptions of each dimension available through tooltips. Adjusting the slider values generates a SAM figure, displayed on the right. Additionally, when slider values are changed, categorical emotion words, based on the theory (Russell et al. 1977), are suggested to describe the corresponding PAD value combination. Users can select one of the suggestions if it is relevant or input their own description.

### Manipulation
During video play, users perform continuous annotation by long-pressing, for a subjective duration, one of the dedicated keys to either raise or lower the curve line. When the key is released, a red line appears to mark the annotation event (see Figure below). When no key is pressed, the curve line continues straight at the last level set after the key was released. At the end of the recording, continuous annotation is terminated and the tool enables navigation between the annotation events (red lines) to assess arousal, pleasure, and dominance using SAM. Users can freely browse the marked events using "previous" and "next" buttons, skipping irrelevant ones. They can also replay 10 seconds of the recording around each event (5 seconds before and after) for reference. The "finish" button becomes enabled at the final event, allowing users to end the annotation process.

<p align="center">
  <img src="affectranktrace_interface.png" alt="AffectRankTrace interface">
</p>

*Figure. Overview of AffectRankTrace interface. The tool was used to load a playback video of palyers' own play session of a sequence of the game "The Evil within" (Bethesda, 2014) and collect data on their affective experience. Players' continuous annotation of Arousal is displayed as a graph beneath the video canvas. The annotation events (red lines) mark moments of increased and decreased arousal. At the end of arousal annotation, the players could navigate through these events to report discrete values for arousal, pleasure, and dominance using corresponding sliders. Adjusting the slider values generates a SAM figure.*

## Requirements

- Windows 10/11
- Microsoft .NET Framework 4.8
- Visual Studio 2022 (or later) with .NET desktop development workload
- VLC media player (required for video playback)

## Dependencies

All required dependencies, including the LibVLC native runtime, are managed through NuGet. Running a package restore (performed automatically by Visual Studio or `nuget restore`) is sufficient before building the solution.

## Installation

1. Clone the repository.
2. Open `AffectRankTrace.sln` in Visual Studio.
3. Restore the NuGet packages (this is performed automatically by default).
4. Build and run the solution.

## Data Storage

The application generates two .csv files stored in a dedicated subdirectory located in the solution root directory `<SolutionRoot>/Annotation_data/`. The solution root is determined automatically from the executable location.

```
<SolutionRoot>/Annotation_data/
├── realtime_data.csv	# Continuous annotation data.
└── discrete_data.csv	# Discrete annotation data.
```

### Data structure

#### realtime_data.csv:

This file contains the continuous annotation of a specified dimension.

The columns are in the following order:

```
----------------------------
timestamp | dimension_value 
----------------------------
```
Description:
timestamp (float): timestamp of the trace's samples in seconds.
dimension_value (int): subjective intensity of the annotated dimension indicated by the user.

#### discrete_data.csv:

This file contains the discrete annotations of Pleasure, Arousal and Dominance at the events marked by the user during the continuous annotation phase. In this context, an event refers to the moment the user started pressing a button to increase or decrease the level of the continuous trace. 

The columns are in the following order:
```
---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
timestamp | arousal_value | arousal_confidence | pleasure_value | pleasure_confidence | dominance_value | dominance_confidence | has_emotion_label | emotion_label | emotion_label_confidence
---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
```
Description:
timestamp (float): timestamp of the marked event in seconds.
arousal/pleasure/dominance value (-3 | -2 | -1 | 0 | 1 | 2 | 3): using the self assessment manikins (SAM) figures.
arousal/pleasure/dominance/emotion_label confidence (0 | 1 | 2 | 3 | 4 | 5): the user's subjective statement of their confidence level when annotating each component, with 1 and 5 respectively denoting the lowest and highest rates, and 0 indicating unrated.
has_emotion_label (bool): True if the user decided to type the emotion temselves, else False. 
emotion_label (string): The label can be typed by the user or picked from the set of emotions suggested by the tool. 

## Citation

If you use this software in your research, please cite:

```bibtex
@inproceedings{graja2024affectranktrace,
  title={AffectRankTrace: a tool for continuous and discrete affective annotation during extended usability trials},
  author={Graja, Sarra and Lovell, P George and Scott-Brown, Ken},
  booktitle={2024 12th International Conference on Affective Computing and Intelligent Interaction (ACII)},
  pages={19--26},
  year={2024},
  organization={IEEE}
}
```
