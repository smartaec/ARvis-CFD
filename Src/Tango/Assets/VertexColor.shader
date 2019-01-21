// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/VertexColor"
{
	Properties{
		point_size("Point Size", Float) = 16.0
		alpha("Alpha", Float) = 0.64
	}
		SubShader
	{
		//Lighting Off
		//ZWrite Off
		Cull Back
		Blend SrcAlpha OneMinusSrcAlpha
		Tags{ "Queue" = "Transparent" "RenderType" = "Transparent" }
		//Tags{ "RenderType" = "Transparent" }

		Pass
	{
		CGPROGRAM
#pragma vertex vert
#pragma fragment frag

		struct VertexInput {
		float4 v : POSITION;
		float4 color: COLOR;
	};

	struct VertexOutput {
		float4 pos : SV_POSITION;
		float4 col : COLOR;
		float size : PSIZE;
	};
	float point_size;
	float alpha;

	VertexOutput vert(VertexInput v) {

		VertexOutput o;
		o.pos = UnityObjectToClipPos(v.v);
		o.size = point_size;
		o.col = v.color;
		o.col.w = alpha;
		return o;
	}

	fixed4 frag(VertexOutput o) : SV_Target{
		return o.col;
	}

		ENDCG
	}
	}
}
