# Video

`teaser.mp4` is the cinematic teaser as the title screen plays it: 1280x720, H.264, no audio (the menu has its own music).
Made from the film's master with:

    ffmpeg -i teaser/output/kartel_teaser_v2.mp4 -an -vf scale=1280:720 -c:v libx264 -profile:v high -level 4.0 \
           -pix_fmt yuv420p -preset slow -crf 25 -maxrate 3M -bufsize 6M -g 48 -movflags +faststart teaser.mp4

`TeaserRT` is the texture MenuSetup points the player and the title's image at.
