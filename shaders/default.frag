#version 330 core
in vec3 fWorldPos;     // World-space position
in vec3 fNormal;       // World-space normal
in vec2 fUv;           // UV coordinates
in float fFaceIndex;   // Face index from vertex shader

// Multiple texture samplers for each face
uniform sampler2D uTexture0;
uniform sampler2D uTexture1;
uniform sampler2D uTexture2;
uniform sampler2D uTexture3;
uniform sampler2D uTexture4;
uniform sampler2D uTexture5;
uniform bool uUseMultiTexture;  // Flag to indicate if we're using multiple textures

// Texture transform uniforms for each face - scale, offset, rotation
uniform vec2 uTexScale[6];
uniform vec2 uTexOffset[6];
uniform float uTexRotation[6];

out vec4 FragColor;

// Helper function to transform texture coordinates based on scale, offset, and rotation
vec2 transformTexCoord(vec2 texCoord, int faceIdx) {
    vec2 centered = texCoord - 0.5;  // Center for rotation
    float cosR = cos(uTexRotation[faceIdx]);
    float sinR = sin(uTexRotation[faceIdx]);
    
    // Apply rotation
    vec2 rotated;
    rotated.x = centered.x * cosR - centered.y * sinR;
    rotated.y = centered.x * sinR + centered.y * cosR;
    
    // Apply scale and offset
    vec2 scaled = rotated / uTexScale[faceIdx];
    return scaled + 0.5 + uTexOffset[faceIdx]; // Return to [0,1] range and add offset
}

// Helper function to get texture based on face index and coordinates
vec4 getTextureForFace(int faceIdx, vec2 texCoord) {
    // Apply texture transformation
    vec2 transformedCoord = transformTexCoord(texCoord, faceIdx);
    
    if (faceIdx == 0) return texture(uTexture0, transformedCoord);
    if (faceIdx == 1) return texture(uTexture1, transformedCoord);
    if (faceIdx == 2) return texture(uTexture2, transformedCoord);
    if (faceIdx == 3) return texture(uTexture3, transformedCoord);
    if (faceIdx == 4) return texture(uTexture4, transformedCoord);
    if (faceIdx == 5) return texture(uTexture5, transformedCoord);
    
    return texture(uTexture0, transformedCoord); // Fallback
}

void main()
{
    vec3 normal = normalize(fNormal);
    vec3 absNormal = abs(normal);
    
    // Calculate blending weights for triplanar mapping
    float blendX = absNormal.x / (absNormal.x + absNormal.y + absNormal.z);
    float blendY = absNormal.y / (absNormal.x + absNormal.y + absNormal.z);
    float blendZ = absNormal.z / (absNormal.x + absNormal.y + absNormal.z);

    // Generate texture coordinates for each axis
    vec2 texCoordX = fWorldPos.yz * 0.5; // Scale texture coordinates
    vec2 texCoordY = fWorldPos.xz * 0.5;
    vec2 texCoordZ = fWorldPos.xy * 0.5;
    
    if (uUseMultiTexture) {
        // When using multiple textures, apply triplanar mapping with different textures
        // Use different textures for positive and negative directions of each axis
        vec4 texNegX, texPosX, texNegY, texPosY, texNegZ, texPosZ;
        
        // Left face (negative X)
        if (normal.x < 0.0) texNegX = getTextureForFace(2, texCoordX);
        else texNegX = vec4(0.0);
        
        // Right face (positive X)
        if (normal.x > 0.0) texPosX = getTextureForFace(3, texCoordX);
        else texPosX = vec4(0.0);
        
        // Bottom face (negative Y)
        if (normal.y < 0.0) texNegY = getTextureForFace(5, texCoordY);
        else texNegY = vec4(0.0);
        
        // Top face (positive Y)
        if (normal.y > 0.0) texPosY = getTextureForFace(4, texCoordY);
        else texPosY = vec4(0.0);
        
        // Back face (negative Z)
        if (normal.z < 0.0) texNegZ = getTextureForFace(1, texCoordZ);
        else texNegZ = vec4(0.0);
        
        // Front face (positive Z)
        if (normal.z > 0.0) texPosZ = getTextureForFace(0, texCoordZ);
        else texPosZ = vec4(0.0);
        
        // Combine all faces based on normal direction
        FragColor = texNegX + texPosX + texNegY + texPosY + texNegZ + texPosZ;
    } else {
        // Original triplanar mapping code for single texture mode
        // Apply transformations to face 0 since we're using a single texture
        vec2 transformed_texCoordX = transformTexCoord(texCoordX, 0);
        vec2 transformed_texCoordY = transformTexCoord(texCoordY, 0);
        vec2 transformed_texCoordZ = transformTexCoord(texCoordZ, 0);
        
        // Sample textures from three axes using the primary texture
        vec4 texX = texture(uTexture0, transformed_texCoordX);
        vec4 texY = texture(uTexture0, transformed_texCoordY);
        vec4 texZ = texture(uTexture0, transformed_texCoordZ);

        // Blend the textures
        FragColor = texX * blendX + texY * blendY + texZ * blendZ;
    }
}