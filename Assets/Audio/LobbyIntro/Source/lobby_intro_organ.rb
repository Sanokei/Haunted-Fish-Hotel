# Haunted Fish Hotel - lobby intro organ cue (Sonic Pi 5)
# Spooky pipe organ, D minor, scored to the LobbyIntroduction.json timeline:
#   0.00 storm city / 1.50 lightning     2.30 knocks (3 x 0.45s)
#   4.15 envelope slides, letter read     13.25 letter fills screen
#  19.25 exterior flash + gate jib       23.05 lobby walk-in
#  28.55 spoken line (music ducks)       33.05 reception
#  37.05 tunnel descent                  40.65 final chord, out by 43
use_bpm 60
use_debug false

define :pipe do |n, dur, amp = 0.3, pan = 0|
  # Drawbar organ: 16', 8', 5 1/3', 4', 2 2/3'
  [[-12, 0.45], [0, 1.0], [7, 0.3], [12, 0.55], [19, 0.25]].each do |off, g|
    synth :sine, note: n + off, attack: 0.03, sustain: [dur - 0.08, 0.01].max,
      release: 0.3, amp: amp * g, pan: pan
  end
  synth :saw, note: n, attack: 0.04, sustain: [dur - 0.08, 0.01].max, release: 0.2,
    amp: amp * 0.06, cutoff: 78, pan: pan
end

define :chord_at do |t, notes, dur, amp|
  at t do
    notes.each { |n| pipe n, dur, amp / Math.sqrt(notes.length) }
  end
end

define :line_at do |t, notes, step, amp, len = nil|
  notes.each_with_index do |n, i|
    next if n == :r
    at t + i * step do
      pipe n, len || step * 0.9, amp
    end
  end
end

with_fx :reverb, room: 0.92, damp: 0.4, mix: 0.42 do
  with_fx :lpf, cutoff: 118 do

    # --- Storm city: low pedal swell, lightning stab at 1.5 -------------
    at 0 do
      synth :sine, note: :d1, attack: 1.4, sustain: 0.6, release: 0.6, amp: 0.55
      synth :sine, note: :a1, attack: 1.4, sustain: 0.6, release: 0.6, amp: 0.25
    end
    chord_at 1.5, [:d3, :f3, :gs3, :b3, :d4], 0.75, 0.75

    # --- Knocks: organ holds its breath, only a soft pedal ---------------
    at 2.3 do
      synth :sine, note: :d1, attack: 0.2, sustain: 1.4, release: 0.4, amp: 0.25
    end

    # --- Envelope + letter: tiptoeing staccato (Luigi's-Mansion sneak) ---
    bass = [:d2, :r, :a2, :r, :f2, :r, :a2, :r, :d2, :r, :a2, :r, :e2, :r, :gs2, :r]
    t = 4.15
    while t < 13.0
      line_at t, bass, 0.2275, 0.32, 0.12
      t += bass.length * 0.2275
    end
    tune = [:a4, :bb4, :a4, :gs4, :a4, :r, :f4, :r,
            :e4, :f4, :e4, :ds4, :e4, :r, :d4, :r,
            :d5, :cs5, :c5, :b4, :bb4, :a4, :gs4, :a4,
            :f4, :a4, :d5, :r, :cs5, :r, :d5, :r]
    line_at 5.06, tune, 0.2275, 0.2, 0.16
    line_at 12.34, [:a4, :gs4], 0.455, 0.18

    # --- Letter fills screen: rising build Dm Bb Gm A7 ------------------
    chord_at 13.25, [:d3, :f3, :a3, :d4], 1.5, 0.45
    chord_at 14.75, [:bb2, :f3, :bb3, :d4], 1.5, 0.5
    chord_at 16.25, [:g2, :d3, :g3, :bb3], 1.5, 0.58
    chord_at 17.75, [:a2, :e3, :g3, :cs4, :e4], 1.5, 0.68
    line_at 18.6, [:a4, :bb4, :b4, :c5, :cs5], 0.13, 0.22

    # --- Exterior: Dracula flourish on the lightning flash (19.25 - 23.05) -----
    line_at 19.25, [:a5, :g5, :a5], 0.11, 0.42, 0.1
    line_at 19.70, [:g5, :f5, :e5, :d5, :cs5, :d5], 0.08, 0.38, 0.08
    chord_at 20.30, [:d2, :d3, :a3, :d4, :f4, :a4], 1.0, 0.95
    line_at 21.00, [:a4, :g4, :a4], 0.1, 0.38, 0.09
    line_at 21.40, [:g4, :f4, :e4, :d4, :cs4, :d4], 0.07, 0.34, 0.07
    chord_at 21.95, [:bb1, :bb2, :d3, :f3, :bb3], 0.55, 0.75
    chord_at 22.50, [:a1, :a2, :cs3, :e3, :g3], 0.55, 0.8

    # --- Lobby (23.05): creeping walk; ducks under the spoken line (28.55 - 33.05)
    walk = [:d2, :a2, :f2, :a2, :d2, :a2, :gs2, :a2]
    t = 23.05
    while t < 37.0
      duck = (t >= 28.4 && t < 33.0) ? 0.45 : 1.0
      line_at t, walk, 0.5, 0.3 * duck, 0.22
      t += walk.length * 0.5
    end
    lobby = [:d4, :r, :f4, :e4, :d4, :r, :cs4, :r,
             :d4, :e4, :f4, :g4, :gs4, :r, :a4, :r,
             :bb4, :a4, :gs4, :a4, :f4, :r, :e4, :r,
             :d4, :r, :r, :r, :a3, :r, :d4, :r]
    line_at 24.55, lobby.take(8), 0.5, 0.22, 0.4
    line_at 33.05, lobby.drop(8).take(8), 0.5, 0.24, 0.4
    chord_at 28.8, [:d3, :f3, :a3], 4.0, 0.16

    # --- Tunnel (37.05 - 40.65): spiralling descent, slowing as it sinks ------
    spiral = [:a5, :f5, :d5, :a4, :gs5, :e5, :d5, :gs4,
              :g5, :e5, :cs5, :g4, :f5, :d5, :a4, :f4,
              :e5, :cs5, :a4, :e4, :d5, :a4, :f4, :d4,
              :cs5, :a4, :e4, :cs4, :bb4, :g4, :d4, :bb3]
    tt = 37.05
    spiral.each_with_index do |n, i|
      step = 0.085 + i * 0.0018
      at tt do
        pipe n, step * 1.4, 0.28 - i * 0.003
      end
      tt += step
    end
    at 37.05 do
      synth :sine, note: :d1, attack: 2.0, sustain: 1.4, release: 0.4, amp: 0.5
    end

    # --- Final chord at 40.65 ------------------------------------------
    chord_at 40.65, [:d2, :a2, :d3, :f3, :a3, :d4], 1.4, 0.85
    at 40.65 do
      synth :sine, note: :d1, attack: 0.05, sustain: 1.2, release: 0.9, amp: 0.55
    end
  end
end

sleep 43.0
