import { defineComponent, h } from 'vue';

export const ItemsStick = defineComponent({
  name: 'ItemsStick',
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
        h('path', {"d": "M4.3 2.20012C4.3 1.81352 4.6134 1.50012 5 1.50012C5.3866 1.50012 5.7 1.81352 5.7 2.20012V7.79983C5.7 8.18643 5.3866 8.49983 5 8.49983C4.6134 8.49983 4.3 8.18643 4.3 7.79983V2.20012Z", "fillRule": "evenodd"})
      ]
    );
  }
});
