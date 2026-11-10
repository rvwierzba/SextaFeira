import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';

export type AgentVisualState = 'IDLE' | 'LISTENING' | 'THINKING' | 'SPEAKING' | 'EXECUTING';

interface HologramCoreCanvasProps {
  state: AgentVisualState;
  audioLevel?: number; // 0.0 to 1.0 from microphone or TTS
}

// 3D Simplex noise GLSL for organic fluid mesh displacement
const vertexShader = `
  uniform float uTime;
  uniform float uAudioLevel;
  uniform float uStateMod;
  varying vec3 vNormal;
  varying vec3 vPosition;
  varying float vDisplacement;

  // Classic Simplex Noise 3D by Stefan Gustavson
  vec4 permute(vec4 x){return mod(((x*34.0)+1.0)*x, 289.0);}
  vec4 taylorInvSqrt(vec4 r){return 1.79284291400159 - 0.85373472095314 * r;}

  float snoise(vec3 v){
    const vec2  C = vec2(1.0/6.0, 1.0/3.0) ;
    const vec4  D = vec4(0.0, 0.5, 1.0, 2.0);
    vec3 i  = floor(v + dot(v, C.yyy) );
    vec3 x0 = v - i + dot(i, C.xxx) ;
    vec3 g = step(x0.yzx, x0.xyz);
    vec3 l = 1.0 - g;
    vec3 i1 = min( g.xyz, l.zxy );
    vec3 i2 = max( g.xyz, l.zxy );
    vec3 x1 = x0 - i1 + 1.0 * C.xxx;
    vec3 x2 = x0 - i2 + 2.0 * C.xxx;
    vec3 x3 = x0 - 1.0 + 3.0 * C.xxx;
    i = mod(i, 289.0 );
    vec4 p = permute( permute( permute(
              i.z + vec4(0.0, i1.z, i2.z, 1.0 ))
            + i.y + vec4(0.0, i1.y, i2.y, 1.0 ))
            + i.x + vec4(0.0, i1.x, i2.x, 1.0 ));
    float n_ = 0.142857142857;
    vec3  ns = n_ * D.wyz - D.xzx;
    vec4 j = p - 49.0 * floor(p * ns.z *ns.z);
    vec4 x_ = floor(j * ns.z);
    vec4 y_ = floor(j - 7.0 * x_ );
    vec4 x = x_ *ns.x + ns.yyyy;
    vec4 y = y_ *ns.x + ns.yyyy;
    vec4 h = 1.0 - abs(x) - abs(y);
    vec4 b0 = vec4( x.xy, y.xy );
    vec4 b1 = vec4( x.zw, y.zw );
    vec4 s0 = floor(b0)*2.0 + 1.0;
    vec4 s1 = floor(b1)*2.0 + 1.0;
    vec4 sh = -step(h, vec4(0.0));
    vec4 a0 = b0.xzyw + s0.xzyw*sh.xxyy ;
    vec4 a1 = b1.xzyw + s1.xzyw*sh.zzww ;
    vec3 p0 = vec3(a0.xy,h.x);
    vec3 p1 = vec3(a0.zw,h.y);
    vec3 p2 = vec3(a1.xy,h.z);
    vec3 p3 = vec3(a1.zw,h.w);
    vec4 norm = taylorInvSqrt(vec4(dot(p0,p0), dot(p1,p1), dot(p2, p2), dot(p3,p3)));
    p0 *= norm.x;
    p1 *= norm.y;
    p2 *= norm.z;
    p3 *= norm.w;
    vec4 m = max(0.6 - vec4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0);
    m = m * m;
    return 42.0 * dot( m*m, vec4( dot(p0,x0), dot(p1,x1), dot(p2,x2), dot(p3,x3) ) );
  }

  void main() {
    vNormal = normal;
    vPosition = position;
    
    // Multi-octave organic undulation
    float freq = 1.2 + uStateMod * 0.5;
    float speed = uTime * (0.8 + uStateMod * 0.4);
    float noise1 = snoise(position * freq + vec3(speed * 0.6));
    float noise2 = snoise(position * (freq * 2.2) - vec3(speed * 0.8)) * 0.45;
    
    // Audio displacement pulse
    float audioAmp = 0.15 + (uAudioLevel * 0.65) + (uStateMod * 0.12);
    float totalDisplacement = (noise1 + noise2) * audioAmp;
    vDisplacement = totalDisplacement;
    
    vec3 newPos = position + normal * totalDisplacement;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(newPos, 1.0);
  }
`;

const fragmentShader = `
  uniform float uTime;
  uniform vec3 uColorBase;
  uniform vec3 uColorGlow;
  uniform float uAudioLevel;
  varying vec3 vNormal;
  varying vec3 vPosition;
  varying float vDisplacement;

  void main() {
    // Fresnel rim glow
    vec3 viewDir = normalize(cameraPosition - vPosition);
    float fresnel = pow(1.0 - max(dot(viewDir, normalize(vNormal)), 0.0), 2.5);
    
    // Dynamic color gradient based on surface displacement
    float pulse = sin(uTime * 2.0) * 0.1 + 0.9;
    vec3 color = mix(uColorBase, uColorGlow, fresnel * 0.8 + (vDisplacement * 1.5 + 0.3) * pulse);
    
    // Core arc brightness
    float brightness = 0.55 + fresnel * 0.65 + uAudioLevel * 0.4;
    gl_FragColor = vec4(color * brightness, 0.72 + fresnel * 0.25);
  }
`;

export const HologramCoreCanvas: React.FC<HologramCoreCanvasProps> = ({ state, audioLevel = 0 }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const stateRef = useRef(state);
  const audioLevelRef = useRef(audioLevel);

  useEffect(() => {
    stateRef.current = state;
  }, [state]);

  useEffect(() => {
    audioLevelRef.current = audioLevel;
  }, [audioLevel]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const width = container.clientWidth;
    const height = container.clientHeight;

    // Scene, Camera, Renderer
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(55, width / height, 0.1, 1000);
    camera.position.z = 6.2;

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    container.appendChild(renderer.domElement);

    // 1. Organic Fluid Orb with Custom Shader
    const sphereGeo = new THREE.SphereGeometry(1.5, 64, 64);
    const shaderUniforms = {
      uTime: { value: 0 },
      uAudioLevel: { value: 0 },
      uStateMod: { value: 0 },
      uColorBase: { value: new THREE.Color(0x0044aa) },
      uColorGlow: { value: new THREE.Color(0x00f0ff) }
    };

    const shaderMaterial = new THREE.ShaderMaterial({
      vertexShader,
      fragmentShader,
      uniforms: shaderUniforms,
      transparent: true,
      wireframe: false,
      blending: THREE.AdditiveBlending
    });

    const fluidOrb = new THREE.Mesh(sphereGeo, shaderMaterial);
    scene.add(fluidOrb);

    // 2. Wireframe Lattice Layer (Geometric Sci-Fi Shielding)
    const wireGeo = new THREE.IcosahedronGeometry(1.58, 3);
    const wireMat = new THREE.MeshBasicMaterial({
      color: 0x00f0ff,
      wireframe: true,
      transparent: true,
      opacity: 0.35,
      blending: THREE.AdditiveBlending
    });
    const wireOrb = new THREE.Mesh(wireGeo, wireMat);
    scene.add(wireOrb);

    // 3. Ultra-Bright Arc-Reactor Core
    const innerCoreGeo = new THREE.SphereGeometry(0.85, 32, 32);
    const innerCoreMat = new THREE.MeshBasicMaterial({
      color: 0x00f0ff,
      transparent: true,
      opacity: 0.75,
      blending: THREE.AdditiveBlending
    });
    const innerCore = new THREE.Mesh(innerCoreGeo, innerCoreMat);
    scene.add(innerCore);

    // 4. Orbiting Celestial Quantum Particles (1200 Points)
    const particleCount = 1200;
    const particleGeometry = new THREE.BufferGeometry();
    const particlePositions = new Float32Array(particleCount * 3);
    const particleOrigins = new Float32Array(particleCount * 3);
    const particleSpeeds = new Float32Array(particleCount);

    for (let i = 0; i < particleCount; i++) {
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(Math.random() * 2 - 1);
      const radius = 2.0 + Math.random() * 2.2;

      const px = radius * Math.sin(phi) * Math.cos(theta);
      const py = radius * Math.sin(phi) * Math.sin(theta);
      const pz = radius * Math.cos(phi);

      particlePositions[i * 3] = px;
      particlePositions[i * 3 + 1] = py;
      particlePositions[i * 3 + 2] = pz;

      particleOrigins[i * 3] = px;
      particleOrigins[i * 3 + 1] = py;
      particleOrigins[i * 3 + 2] = pz;

      particleSpeeds[i] = 0.4 + Math.random() * 1.2;
    }

    particleGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3));
    const particleMaterial = new THREE.PointsMaterial({
      color: 0x00ffff,
      size: 0.05,
      transparent: true,
      opacity: 0.85,
      blending: THREE.AdditiveBlending
    });
    const particleField = new THREE.Points(particleGeometry, particleMaterial);
    scene.add(particleField);

    // 5. Gyroscopic Concentric Arc Rings with Sci-Fi Reticles
    const createRing = (radius: number, tubeRadius: number, color: number, rx: number, ry: number) => {
      const ringGeo = new THREE.TorusGeometry(radius, tubeRadius, 3, 100);
      const ringMat = new THREE.MeshBasicMaterial({
        color,
        wireframe: true,
        transparent: true,
        opacity: 0.45,
        blending: THREE.AdditiveBlending
      });
      const mesh = new THREE.Mesh(ringGeo, ringMat);
      mesh.rotation.x = rx;
      mesh.rotation.y = ry;
      return mesh;
    };

    const ring1 = createRing(2.5, 0.015, 0x00f0ff, Math.PI / 2.2, 0);
    const ring2 = createRing(2.95, 0.012, 0x0077ff, Math.PI / 3.5, Math.PI / 4);
    const ring3 = createRing(3.4, 0.018, 0x00f0ff, -Math.PI / 4, Math.PI / 5);
    scene.add(ring1);
    scene.add(ring2);
    scene.add(ring3);

    // Resize Handler
    const handleResize = () => {
      if (!container) return;
      const w = container.clientWidth;
      const h = container.clientHeight;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h);
    };
    window.addEventListener('resize', handleResize);

    // Interactive mouse parallax
    let mouseX = 0;
    let mouseY = 0;
    const handleMouseMove = (e: MouseEvent) => {
      mouseX = (e.clientX / window.innerWidth - 0.5) * 1.5;
      mouseY = (e.clientY / window.innerHeight - 0.5) * 1.5;
    };
    window.addEventListener('mousemove', handleMouseMove);

    // Animation Loop
    let animationFrameId: number;
    let clock = new THREE.Clock();

    const animate = () => {
      animationFrameId = requestAnimationFrame(animate);
      const elapsedTime = clock.getElapsedTime();
      const currentState = stateRef.current;
      const currentAudio = audioLevelRef.current;

      // Update Shader Uniforms
      shaderUniforms.uTime.value = elapsedTime;
      shaderUniforms.uAudioLevel.value = currentAudio;

      let stateMod = 0;
      let rotSpeed = 0.35;
      let baseTargetScale = 1.0;

      if (currentState === 'IDLE') {
        stateMod = 0.0;
        rotSpeed = 0.25;
        baseTargetScale = 1.0 + Math.sin(elapsedTime * 1.8) * 0.04;
        shaderUniforms.uColorBase.value.setHex(0x003388);
        shaderUniforms.uColorGlow.value.setHex(0x00f0ff);
        particleMaterial.color.setHex(0x00ffff);
      } else if (currentState === 'LISTENING') {
        stateMod = 0.8;
        rotSpeed = 0.5;
        baseTargetScale = 1.12 + (currentAudio * 0.4) + Math.sin(elapsedTime * 5.0) * 0.06;
        shaderUniforms.uColorBase.value.setHex(0x0055bb);
        shaderUniforms.uColorGlow.value.setHex(0x33ffff);
        particleMaterial.color.setHex(0x33ffff);
      } else if (currentState === 'THINKING' || currentState === 'EXECUTING') {
        stateMod = 1.8;
        rotSpeed = 1.4;
        baseTargetScale = 1.08 + Math.sin(elapsedTime * 8.0) * 0.08;
        shaderUniforms.uColorBase.value.setHex(0x884400);
        shaderUniforms.uColorGlow.value.setHex(0xffaa00);
        particleMaterial.color.setHex(0xffaa00);
      } else if (currentState === 'SPEAKING') {
        stateMod = 1.2;
        rotSpeed = 0.6;
        baseTargetScale = 1.1 + (currentAudio * 0.55) + Math.sin(elapsedTime * 9.0) * 0.1;
        shaderUniforms.uColorBase.value.setHex(0x0044aa);
        shaderUniforms.uColorGlow.value.setHex(0x00f0ff);
        particleMaterial.color.setHex(0x00f0ff);
      }

      shaderUniforms.uStateMod.value = stateMod;

      // Smooth Scale Lerp
      const currentScale = fluidOrb.scale.x;
      const newScale = THREE.MathUtils.lerp(currentScale, baseTargetScale, 0.1);
      fluidOrb.scale.set(newScale, newScale, newScale);
      wireOrb.scale.set(newScale * 1.05, newScale * 1.05, newScale * 1.05);
      innerCore.scale.set(newScale * 0.85, newScale * 0.85, newScale * 0.85);

      // Core Rotations
      fluidOrb.rotation.y = elapsedTime * rotSpeed * 0.7;
      fluidOrb.rotation.x = Math.sin(elapsedTime * 0.3) * 0.2;
      wireOrb.rotation.y = -elapsedTime * rotSpeed * 0.5;
      wireOrb.rotation.z = elapsedTime * rotSpeed * 0.3;
      innerCore.rotation.y = elapsedTime * rotSpeed;

      // Gyroscopic Ring Rotations
      ring1.rotation.z = elapsedTime * rotSpeed * 0.45;
      ring2.rotation.x = elapsedTime * rotSpeed * 0.35;
      ring2.rotation.z = -elapsedTime * rotSpeed * 0.25;
      ring3.rotation.y = elapsedTime * rotSpeed * 0.5;

      // Animate Particles
      const positions = particleGeometry.attributes.position.array as Float32Array;
      for (let i = 0; i < particleCount; i++) {
        const ox = particleOrigins[i * 3];
        const oy = particleOrigins[i * 3 + 1];
        const oz = particleOrigins[i * 3 + 2];
        const spd = particleSpeeds[i];

        // Orbit around Y axis with slight audio expansion
        const angle = elapsedTime * 0.2 * spd + (i * 0.01);
        const cosA = Math.cos(angle);
        const sinA = Math.sin(angle);
        const expansion = 1.0 + (currentAudio * 0.35);

        positions[i * 3] = (ox * cosA - oz * sinA) * expansion;
        positions[i * 3 + 1] = (oy + Math.sin(elapsedTime * spd + i) * 0.15) * expansion;
        positions[i * 3 + 2] = (ox * sinA + oz * cosA) * expansion;
      }
      particleGeometry.attributes.position.needsUpdate = true;

      // Camera parallax tilt towards mouse
      camera.position.x = THREE.MathUtils.lerp(camera.position.x, mouseX * 0.8, 0.05);
      camera.position.y = THREE.MathUtils.lerp(camera.position.y, -mouseY * 0.8, 0.05);
      camera.lookAt(0, 0, 0);

      renderer.render(scene, camera);
    };

    animate();

    return () => {
      cancelAnimationFrame(animationFrameId);
      window.removeEventListener('resize', handleResize);
      window.removeEventListener('mousemove', handleMouseMove);
      renderer.dispose();
      sphereGeo.dispose();
      shaderMaterial.dispose();
      wireGeo.dispose();
      wireMat.dispose();
      innerCoreGeo.dispose();
      innerCoreMat.dispose();
      particleGeometry.dispose();
      particleMaterial.dispose();
      if (container.contains(renderer.domElement)) {
        container.removeChild(renderer.domElement);
      }
    };
  }, []);

  return (
    <div
      ref={containerRef}
      style={{
        position: 'absolute',
        top: 0,
        left: 0,
        width: '100%',
        height: '100%',
        zIndex: 4,
        pointerEvents: 'none'
      }}
    />
  );
};
