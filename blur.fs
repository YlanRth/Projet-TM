#version 330

in vec2 fragTexCoord;
in vec4 fragColor;

out vec4 finalColor;

uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec2 renderSize; // Taille de la fenêtre (ex: 1920, 1080)

void main()
{
    vec2 texelSize = 1.0 / renderSize;
    vec4 sum = vec4(0.0);
    
    // Rayon du flou
    int radius = 4;
    int count = 0;

    for (int x = -radius; x <= radius; x++)
    {
        for (int y = -radius; y <= radius; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize * 1.5;
            sum += texture2D(texture0, fragTexCoord + offset);
            count++;
        }
    }

    finalColor = (sum / float(count)) * colDiffuse * fragColor;
}