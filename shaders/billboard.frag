#version 410 core

in vec2 TexCoord;
out vec4 FragColor;

uniform vec4 billboardColor;

void main()
{
    FragColor = billboardColor;
}