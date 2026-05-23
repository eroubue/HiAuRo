import { defineComponent, h } from 'vue';

export const MediaTechnologyRecording = defineComponent({
  name: 'MediaTechnologyRecording',
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
        
      ]
    );
  }
});
