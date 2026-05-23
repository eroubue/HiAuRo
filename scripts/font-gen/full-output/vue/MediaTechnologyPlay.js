import { defineComponent, h } from 'vue';

export const MediaTechnologyPlay = defineComponent({
  name: 'MediaTechnologyPlay',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M6.25925 3.6287C7.05468 4.22127 7.4524 4.51756 7.48962 4.91637C7.49481 4.972 7.49481 5.028 7.48962 5.08363C7.4524 5.48244 7.05468 5.77873 6.25925 6.3713L5.77501 6.73205C5.01262 7.3 4.63143 7.58398 4.31216 7.57055C4.05461 7.55971 3.81407 7.43895 3.65151 7.23889C3.45001 6.99089 3.45001 6.51554 3.45001 5.56486V5V4.43514C3.45001 3.48446 3.45001 3.00911 3.65151 2.76111C3.81407 2.56105 4.05461 2.44029 4.31216 2.42945C4.63143 2.41602 5.01262 2.7 5.77501 3.26795L6.25925 3.6287Z", "fillRule": "evenodd"})
      ]
    );
  }
});
