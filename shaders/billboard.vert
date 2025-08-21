#version 410 core

layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;
uniform vec2 billboardSize;
uniform vec2 viewportSize;
uniform float rotation;
uniform float zOffset;

out vec2 TexCoord;

void main()
{
    // Extract camera right and up vectors from the view matrix
    vec3 camRight = vec3(view[0][0], view[1][0], view[2][0]);
    vec3 camUp = vec3(view[0][1], view[1][1], view[2][1]);
    
    // Billboard position in world space
    vec3 worldPos = vec3(model[3]);
    
    // Calculate vertex position in clip space
    vec4 clipPos = projection * view * vec4(worldPos, 1.0);
    
    // Apply rotation if needed
    float cosR = cos(radians(rotation));
    float sinR = sin(radians(rotation));
    vec2 rotatedPos = vec2(
        aPos.x * cosR - aPos.y * sinR,
        aPos.x * sinR + aPos.y * cosR
    );
    
    // Convert from pixel to NDC coordinates
    vec2 ndcSize = billboardSize / viewportSize;
    
    // Apply the billboard offset in NDC space
    clipPos.xy += rotatedPos * ndcSize * clipPos.w;
    
    // Apply Z-offset for layering (manual depth adjustment)
    // Only for billboards with depth testing disabled
    clipPos.z -= zOffset * 0.0001; // Small factor to avoid large offsets
    
    gl_Position = clipPos;
    TexCoord = aTexCoord;
}